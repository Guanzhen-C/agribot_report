# HPM6750 VCU 串口配置固件

该固件在 HPM6750EVK2 Core0 上使用 UART0（FireDAP 虚拟串口，115200 8N1）接收
`VCFG/1` 配置帧，只允许写入已编译的算力接口、农机和协议组合：

- `machine_id = 0x0001` / `protocol_id = 0x0101`：禾川履带农机，250kbps
- `machine_id = 0x0010/0x0011/0x0013/0x0014` / `protocol_id = 0x0201`：禾川差速、四驱、横移、全向农机，1Mbps
- `machine_id = 0x0012` / `protocol_id = 0x0202`：禾川转向农机，1Mbps
- `compute_interface_id = 0`：UART13 串口
- `compute_interface_id = 1`：CAN1，算力侧使用原始 CAN API
- `compute_interface_id = 2`：CAN1，算力侧使用 Linux SocketCAN

配置记录写在板载 QSPI NOR 的最后两个扇区。A/B 槽均包含 schema、递增代数和
CRC32；启动时选择最新的有效记录。写入非活动槽失败或中途掉电时，旧槽仍有效。
相同配置重复写入是幂等操作，不增加代数，也不擦写 Flash。

选择 CAN 或 SocketCAN 后，固件下次启动会把 `PE31/J11[7]` 路由为 CAN1.TXD、
`PE30/J11[10]` 路由为 CAN1.RXD。EVK2 的 CAN1 没有板载收发器，必须外接 3.3V
CAN 收发器；原板载 CAN0 保留给农机总线。当前固件仅完成选择持久化和引脚路由，
FGCP 指令接收及 CAN0 协议转换仍待实现。

新增 `hechuan_motion.c` 是平台无关的协议编解码模块，已加入构建，尚未接入主循环的持续运动收发。`vcu_machine_profile.c` 提供型号匹配校验和目标 CAN 速率查询；后者尚未用于初始化 CAN 控制器。分析与安全边界见 `../../docs/HECHUAN_PROTOCOL_ANALYSIS.md`。

本次 2026-09-08 修改须重新烧录及进行安全台架验证。文末旧版本的实板记录不构成本版本验证。

## 构建

```sh
source ./env.sh
cmake -S firmware/vcu_configurator \
  -B build/vcu_configurator -GNinja \
  -DBOARD=hpm6750evk2 \
  -DCMAKE_BUILD_TYPE=debug \
  -DHPM_BUILD_TYPE=ram \
  -DCMAKE_POLICY_VERSION_MINIMUM=3.5
cmake --build build/vcu_configurator
```

## 主机单测

Windows（提供可用的 GCC 路径）：

```powershell
./firmware/vcu_configurator/tests/run_host_tests.ps1 -Compiler C:/Qt/Tools/mingw1310_64/bin/gcc.exe
```

Linux / macOS：

```sh
sh firmware/vcu_configurator/tests/run_host_tests.sh
```

## FireDAP 实板测试

固件已烧入 Flash 并运行时：

```sh
python3 firmware/vcu_configurator/scripts/test_vcu_serial.py \
  /dev/cu.wchusbserial130 --compute-interface socketcan
```

脚本检查 ACK 的 magic、版本、opcode、sequence 和 CRC，并以 JSON 输出活动槽、
配置代数及实际生效 ID。复位后再次发送同一选择，若槽位和代数均不变，即证明固件
从 Flash 恢复了配置且幂等写入生效。

FireDAP 在 Mac 打开虚拟串口时会通过 DTR 复位目标板。测试 RAM 镜像时应先运行
带延迟的脚本并保持端口打开，再在延迟期间由 OpenOCD 下载和运行镜像：

```sh
python3 firmware/vcu_configurator/scripts/test_vcu_serial.py \
  /dev/cu.usbmodem21301 --send-delay 8
```

另一个终端须在 8 秒内执行 RAM 下载命令。直接先下载 RAM 镜像、再打开串口会让
CPU 回到板上原有的 Flash 程序。

串口传输期间固件不输出文本日志，避免日志字节污染二进制 ACK。

### UART0 接线限制

HPM6750EVK2 的 UART0 同时引到 JTAG P4、J109 和板载 USB2UART0。只能让其中一路
USB-UART 的 TX 驱动 `UART0.RX/PY07`；FireDAP 虚拟串口与板载 USB2UART0 同时
连接会造成接收丢失或乱码。使用板载 USB2UART0 时，应断开 FireDAP 排线上对应
`P4[9] / UART0.RX` 的 V-UART 发送线，JTAG 其余信号可保留。

## Flash 后端自测镜像

当串口硬件尚未接通时，可构建仅用于台架验证的自测镜像。它启动后通过与正式固件
完全相同的 A/B 存储路径写入内置组合，再回读校验：

```sh
cmake -S firmware/vcu_configurator \
  -B build/vcu_configurator-selftest -GNinja \
  -DBOARD=hpm6750evk2 -DHPM_BUILD_TYPE=ram \
  -DVCU_CONFIG_SELF_TEST=1 \
  -DCMAKE_POLICY_VERSION_MINIMUM=3.5
cmake --build build/vcu_configurator-selftest
```

`VCU_CONFIG_SELF_TEST` 不应在生产镜像中开启。

## HPM6750EVK2 实板验证

2026-09-02 使用 FireDAP JTAG 下载 RAM 镜像、板载 WCH USB2UART0 传输配置完成验证：

- 从全擦除的配置区首次写入，ACK 返回 `status=0`、槽 A、代数 1；
- 槽 A 原始地址 `0x80FFE000`，记录为 `VCUS`、schema 1、长度 24，CRC32 正确；
- 复位并重新加载固件后仍返回槽 A、代数 1；
- 重复写入同一配置后，最后 8 KiB Flash 与写入前逐字节相同；
- 未支持的机器 ID 返回状态 3，CRC 损坏帧返回状态 1，活动配置保持不变。

同日扩展算力接口配置后再次完成实板验证：

- 写入 `compute_interface_id=2`（SocketCAN/CAN1）成功，活动槽 B、代数 2；
- 复位后重复写入仍为槽 B、代数 2，未发生重复擦写；
- Flash 槽 B 地址 `0x80FFF000` 的接口字段读回为 2；
- IOC 寄存器读回确认 PE30、PE31 的 ALT_SELECT 均为 7，即 CAN1 功能；
- 更新后的 Flash XIP 镜像已由 OpenOCD 编程并通过 verify，复位后 VCFG ACK 正常。
