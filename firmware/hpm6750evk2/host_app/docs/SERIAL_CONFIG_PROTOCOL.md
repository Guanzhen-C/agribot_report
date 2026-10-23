# VCU 串口配置协议 VCFG/1

用途：仅写入“算力输入接口 + 农机类型 + CAN 协议选择”。不承载车辆驾驶命令和实时遥测。

串口默认参数：`115200, 8N1`。所有多字节整数均为小端序。

## 1. 通用帧

| 偏移 | 长度 | 字段 | 说明 |
| ---: | ---: | --- | --- |
| 0 | 4 | magic | ASCII `VCFG`，字节 `56 43 46 47` |
| 4 | 1 | version | 固定为 `1` |
| 5 | 1 | opcode | 请求 `0x10`，ACK `0x90` |
| 6 | 2 | payload_length | 当前固定为 `8` |
| 8 | 4 | sequence | 上位机递增序号，ACK 原样返回 |
| 12 | 8 | payload | 见下文 |
| 20 | 4 | crc32 | IEEE CRC32，覆盖偏移 0～19 |

每帧固定 24 字节。CRC32 多项式为 `0xEDB88320`，初值和结果异或值均为
`0xFFFFFFFF`。

## 2. 写入农机与协议

Opcode：`0x10 WRITE_SELECTION`

| Payload 偏移 | 长度 | 字段 | 当前值 |
| ---: | ---: | --- | --- |
| 0 | 2 | machine_id | 见下方型号注册表 |
| 2 | 2 | protocol_id | 由型号匹配，不由普通用户手选 |
| 4 | 1 | compute_interface_id | 0=串口；1=CAN；2=SocketCAN |
| 5 | 3 | reserved | 固定为 0 |

| 型号 | machine_id | protocol_id | 设备总线速率 |
| --- | --- | --- | --- |
| 禾川履带农机 | `0x0001` | `0x0101` | 250kbps |
| 禾川差速农机 | `0x0010` | `0x0201` | 1Mbps |
| 禾川四驱农机 | `0x0011` | `0x0201` | 1Mbps |
| 禾川转向农机 | `0x0012` | `0x0202` | 1Mbps |
| 禾川横移农机 | `0x0013` | `0x0201` | 1Mbps |
| 禾川全向农机 | `0x0014` | `0x0201` | 1Mbps |

速率是运行协议要求，保存配置不会立即启动或发送运动数据。

VCU 收到请求后必须依次执行：

以下包含运行固件的安全要求。当前配置固件尚无实时停车检测与执行器联锁，第 2 项不能视为已实现；上位机的停车勾选是操作确认，不是传感器检测。

1. 检查 magic、版本、长度、opcode 和 CRC32；
2. 检查 VCU 当前处于 `STANDBY` 且驱动输出为零；
3. 检查 `compute_interface_id`、`machine_id`、`protocol_id` 均受支持；
4. 若选择与当前配置相同，直接返回当前槽位和代数，不擦写 Flash；
5. 否则写入非活动 Flash 配置槽；
6. 回读并校验 magic、schema、配置代数和 CRC32；
7. 返回 ACK。启动时以 CRC 有效且 generation 较新的槽作为活动槽。

收到配置不应自动使能车辆输出。建议新选择在下次 VCU 重启后生效；若支持即时
切换，也只能在 `STANDBY` 状态完成协议模块重初始化。

## 3. 写入确认

Opcode：`0x90 WRITE_SELECTION_ACK`

| Payload 偏移 | 长度 | 字段 | 说明 |
| ---: | ---: | --- | --- |
| 0 | 1 | status | 0 表示成功 |
| 1 | 1 | active_slot | 0=A，1=B |
| 2 | 2 | generation | 持久化配置代数低 16 bit |
| 4 | 2 | machine_id | 实际写入的农机 ID |
| 6 | 2 | protocol_id | 实际写入的协议 ID |

v1 ACK 不回显 compute_interface_id；当前由固件对完整记录回读校验后报告成功，上位机不能仅凭 ACK 独立核对该字段。如需独立读回，应另增查询 opcode，不能改变现有 ACK 布局。

状态码：

| 值 | 名称 | 含义 |
| ---: | --- | --- |
| 0 | `OK` | 写入、回读和激活成功 |
| 1 | `BAD_FRAME` | 帧格式或 CRC 错误 |
| 2 | `UNSUPPORTED_VERSION` | 不支持该帧版本 |
| 3 | `UNSUPPORTED_MACHINE` | 未编译该农机 |
| 4 | `UNSUPPORTED_PROTOCOL` | 未编译该协议 |
| 5 | `INCOMPATIBLE_SELECTION` | 农机和协议组合不兼容 |
| 6 | `VCU_NOT_STANDBY` | 车辆未停车或输出未禁用 |
| 7 | `FLASH_WRITE_FAILED` | Flash 擦写失败 |
| 8 | `VERIFY_FAILED` | Flash 回读/CRC 校验失败 |
| 9 | `UNSUPPORTED_COMPUTE_INTERFACE` | 不支持所选算力输入接口 |

即使请求帧 CRC 错误，只要能可靠解析 sequence，VCU 也应返回状态 1；无法确定
sequence 时直接丢弃，避免对噪声作出响应。

## 4. 持久化记录建议

Flash A/B 槽中的最小记录：

```c
typedef struct {
    uint32_t magic;          // "VCUS"
    uint16_t schema_version; // 1
    uint16_t length;
    uint32_t generation;
    uint16_t machine_id;
    uint16_t protocol_id;
    uint8_t compute_interface_id;
    uint8_t reserved[3];
    uint32_t crc32;
} vcu_selection_record_t;
```

完整记录保存在 QSPI NOR 的最后两个物理扇区，每槽一个记录。无需单独的活动槽
标记：启动时同时检查 A/B 两槽，选择 CRC 正确且 generation 最新的记录。写新槽
不会破坏旧槽，因此任意一次擦写中断后仍至少保留旧配置。两槽都无效时进入未配置
状态，VCU 必须保持零输出。

## 5. 兼容性规则

- 已发布的 `machine_id` 和 `protocol_id` 永不复用。
- 新增农机或协议只增加 ID，不改变 v1 帧布局。
- 当 16 bit ID 空间不足或需要更多配置字段时，新增 opcode，不改变 `0x10`。
- 上位机注册表与 VCU 固件注册表必须来自同一份受版本控制的清单。
