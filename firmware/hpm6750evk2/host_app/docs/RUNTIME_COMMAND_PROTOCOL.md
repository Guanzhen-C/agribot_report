# 算力平台运行指令协议 FGCP/1

状态：算力接口选择、VCFG 配置和 CAN1/UART13 引脚路由已定义；运行指令解析与
CAN 转换任务尚待接入 HPM6750 固件。

## 1. 系统链路

| 链路 | VCU 端口 | 协议 | 用途 |
| --- | --- | --- | --- |
| Mac/PC → VCU | UART0 / USB-UART | VCFG/1 | 停车时写入算力接口、农机和协议选择 |
| 算力平台 → VCU | CAN1 或 UART13 | FGCP/1 | 运行时持续发送标准驾驶指令 |
| VCU → 农机 | 板载收发器 CAN0 | 所选农机 DBC | 转换为具体农机的控制报文 |

算力输入可以持久化选择：

| ID | 页面选项 | VCU 物理端口 | 算力平台软件接口 |
| ---: | --- | --- | --- |
| 0 | 串口 | UART13 | `/dev/ttyUSB*` 或原生 UART |
| 1 | CAN | CAN1 | 厂商 CAN API 或原始 CAN 驱动 |
| 2 | SocketCAN | CAN1 | Linux `can0` / PF_CAN |

CAN 与 SocketCAN 在 VCU 一侧完全相同，区别只在算力平台的软件 API。两者均使用
CAN1 和相同的 FGCP CAN 报文。CAN0 与 CAN1 是两条独立总线，不能把两组 CANH、
CANL 并接。

## 2. CAN1 硬件引出

HPM6750EVK2 没有为 CAN1 安装第二颗板载收发器。J11 只引出 MCU 的 3.3V 逻辑
TXD/RXD，必须先接外部 CAN 收发器，不能直接接 CANH/CANL。

| HPM6750EVK2 | 功能 | 外部 3.3V CAN 收发器 |
| --- | --- | --- |
| J11[7] / PE31 | CAN1.TXD | TXD / D |
| J11[10] / PE30 | CAN1.RXD | RXD / R |
| J11[2] | 3V3 | VCC，仅适用于 3.3V 模块 |
| J11[1] | GND | GND |
| — | — | CANH/CANL 接算力平台 CAN 适配器 |

台架可使用 SN65HVD230 3.3V 模块。两端共地，CANH/CANL 使用双绞线，总线只在
物理两端各保留一个 120Ω 终端。整条总线断电时测 CANH-CANL 应约为 60Ω。
SN65HVD230 模块仅用于台架验证；量产硬件应按车辆电气环境增加隔离或共模、浪涌、
ESD、防反接和 EMC 防护。

## 3. UART13 备选接线

串口模式使用 `115200, 8N1`、无硬件流控的 3.3V TTL UART：

| 算力平台/USB-UART | HPM6750EVK2 | MCU 引脚 |
| --- | --- | --- |
| TX | J11[6] UART13.RXD | PZ08 |
| RX | J11[5] UART13.TXD | PZ09 |
| GND | J11[1] GND | GND |

TX/RX 必须交叉，不连接 USB-UART 的 VCC。不能把 RS-232 的正负电压直接接入 MCU。

## 4. ROS 2 输入

算力平台适配节点订阅：

| Topic | Type | 使用字段 |
| --- | --- | --- |
| `/cmd_vel` | `geometry_msgs/msg/Twist` | `linear.x`，m/s；`angular.z`，rad/s |
| `/fieldgate/implement_cmd` | `fieldgate_msgs/msg/ImplementCommand` | 使能、软件停车、灯光、风机、水泵、碎枝 |

ROS 适配节点按照用户选择，把同一组标准指令编码成 CAN、SocketCAN 或串口帧。
VCU 固件不依赖 ROS 2 或 micro-ROS。

```text
bool enable
bool stop_request
bool headlight
uint8 fan_gear                 # 0..8
uint8 pump_gear                # 0..8
float32 shredder_percent       # 0.00..100.00
```

## 5. CAN / SocketCAN 传输

物理端口为 CAN1，经典 CAN，500kbps，标准 11-bit ID。两帧均以 20Hz 周期发送。

### `0x180 DRIVE_COMMAND`

| Byte | 字段 | 编码 |
| ---: | --- | --- |
| 0..1 | linear_x_mm_s | int16，小端，`linear.x × 1000` |
| 2..3 | angular_z_mrad_s | int16，小端，`angular.z × 1000` |
| 4 | flags | bit0 enable；bit1 stop_request；bit2 headlight |
| 5 | sequence | uint8，每帧递增 |
| 6 | valid_for_10ms | v1 固定为 30，即 300ms |
| 7 | crc8 | CRC-8/SAE-J1850，覆盖 Byte0..6 |

### `0x181 IMPLEMENT_COMMAND`

| Byte | 字段 | 编码 |
| ---: | --- | --- |
| 0 | fan_gear | 0=关闭，1..8=档位 |
| 1 | pump_gear | 0=关闭，1..8=档位 |
| 2..3 | shredder_percent_x100 | uint16，小端，0..10000 |
| 4 | reserved | 固定为 0 |
| 5 | sequence | 与同周期 `0x180` 相同 |
| 6 | valid_for_10ms | v1 固定为 30 |
| 7 | crc8 | CRC-8/SAE-J1850，覆盖 Byte0..6 |

SocketCAN 初始化示例：

```sh
sudo ip link set can0 down
sudo ip link set can0 type can bitrate 500000 restart-ms 100
sudo ip link set can0 up
```

## 6. 串口传输

所有多字节整数均为小端序。每帧固定 32 字节，默认以 20Hz 发送。

| 偏移 | 长度 | 字段 | 说明 |
| ---: | ---: | --- | --- |
| 0 | 2 | magic | ASCII `FG`，字节 `46 47` |
| 2 | 1 | version | 固定为 1 |
| 3 | 1 | message_type | `0x10`：`DRIVE_COMMAND` |
| 4 | 2 | frame_length | 固定为 32 |
| 6 | 2 | sequence | 每帧递增，允许回绕 |
| 8 | 4 | source_time_ms | 算力平台单调时钟低 32 bit |
| 12 | 4 | linear_x_mm_s | `linear.x × 1000` |
| 16 | 4 | angular_z_mrad_s | `angular.z × 1000` |
| 20 | 2 | flags | bit0 enable；bit1 stop_request；bit2 headlight |
| 22 | 1 | fan_gear | 0=关闭，1..8=档位 |
| 23 | 1 | pump_gear | 0=关闭，1..8=档位 |
| 24 | 2 | shredder_percent_x100 | 0..10000 |
| 26 | 2 | valid_for_ms | v1 固定为 300 |
| 28 | 4 | crc32 | IEEE CRC32，覆盖字节 0..27 |

## 7. 失效安全

- VCU 连续 300ms 未收到完整且有效的一组 FGCP 指令时，全部软件输出归零；
- CAN 模式同时检查 CAN 帧 CRC8、成对序号、字段范围和接收超时；
- 串口模式检查 magic、版本、长度、CRC32、序号、范围和接收超时；
- `stop_request=1` 或 `enable=0` 时，VCU 只向农机发送零输出；
- 算力进程重启、总线断开或校验连续错误不能保持上一次非零指令。

`stop_request` 不能代替车辆独立、硬接线的急停回路。

## 8. 三合一农机 CAN v1 映射

VCU 使用 `machine_id=0x0001`、`protocol_id=0x0101` 选择适配器：

| 方向 | CAN0 ID | 报文 | 周期 | 主要字段 |
| --- | ---: | --- | ---: | --- |
| VCU → 农机行走机构 | `0x514` | 行走控制 | 200ms | 左/右电机百分比、大灯、counter、XOR checksum |
| VCU → 作业控制器 | `0x580` | `WorkDev_Ctrl` | 200ms | 风机、水泵、碎枝、counter、XOR checksum |
| 农机 → VCU | `0x532` | 行走状态 | 500ms | 模式、急停、运行、电压和通信故障 |
| 农机 → VCU | `0x533/0x534` | `MCUL/MCUR_Info` | 500ms | 左右电机与故障状态 |
| 作业控制器 → VCU | `0x5B0` | `WorkDev_State` | 500ms | 作业设备状态 |

CAN0 使用 250kbps。原始 DBC 要求控制帧间隔 100–300ms；超过 500ms 未收到控制帧
时农机停止运行。VCU 根据所选农机参数，把标准线速度和角速度转换成左右电机百分比。
