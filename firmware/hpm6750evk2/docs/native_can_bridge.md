# 原生 CAN 桥接固件

保留 FieldGate VCU 原作者刘泽禹、原有机器和协议 ID。本扩展不采用旧 FGCP 控制输入，
不修改 Agribot 上位控制算法，不在 VCU 内重新计算速度、转角或百分比。

| 预置镜像 | machine / protocol | CAN0、CAN1 | CAN1 → CAN0 | CAN0 → CAN1 |
| --- | --- | --- | --- | --- |
| ackermann | 0x0012 / 0x0202 | 1 Mbps | 0x181 | 0x101、0x102、0x103 |
| tracked | 0x0001 / 0x0101 | 250 kbps | 0x514 | 0x532、0x533、0x534 |

两侧为经典 CAN、标准 11 位数据帧、DLC=8。扩展帧、远程帧、CAN FD 和反向控制帧拒绝转发。
CAN1 使用 PE31/PE30；CAN0 使用板卡 CAN0 接口。两端都需要物理 CAN 收发器和正确共地。
转发不依赖三条反馈齐全，原有 RDK/执行器控制授权仍由其自身负责。
反馈 CRC 错误的数据仍原样转发供 RDK诊断，但不生成有效电压/速度遥测。

每个方向 16 帧队列，接收/发送单次预算各 8 帧；发送忙时保留待发帧，溢出丢最旧帧。
阿克曼指令队列期限 250 ms，履带指令和反馈期限 600 ms。计时支持 uint32 回绕。
成功发送即出队，没有周期性重放运动指令，没有自动构造额外停止帧。
必须通过实板断链测试确认执行器自己的控制超时停车。

## 构建

使用官方 HPM SDK v1.12.1 和 RISC-V GNU 13.2.0。两者放在忽略的 `.hpm` 内，
也可通过 `HPM_SDK_BASE` 和 `GNURISCV_TOOLCHAIN_PATH` 指定外部安装。

```bash
source env.sh
python3 scripts/build_native_bridge.py --profile ackermann
python3 scripts/build_native_bridge.py --profile tracked
sh firmware/vcu_configurator/tests/run_host_tests.sh
cd host_app
node --test tests/native-telemetry.test.mjs
```

生成位置 `build/releases/{ackermann,tracked}`，包括 BIN、ELF 和 SHA-256 清单。
只构建，不调用烧录工具。Flash XIP 镜像启动时以编译预置的配置为准，旧 A/B 配置不覆盖，
也不被清空；预置镜像始终拒绝写配置。`VCU_BRIDGE_MACHINE_ID=0` 保留维护镜像的原有流程。
不要全片擦除末尾配置扇区；人工烧录先核对板卡和 ELF 地址，架空验证后方可使用。

## 只读诊断

UART0 115200 8N1。24 字节 VCFG v1 查询：0x21 身份、0x20 状态；头部序号原样回传，CRC32。
身份回复 0xA1：版本三字节、flags（bit0 硬件就绪、bit1 原生桥接）、机器ID、协议ID。
状态回复 0xA0，80 字节；原生 payload schema=2。多字节遥测字段均小端。
payload 偏移：0 schema，1 配置/输入/反馈/硬件 flags，2 fault，4 machine，6 protocol，8 interface，
10 有效位（电压/车速/左RPM/右RPM），12 uptime，16 command age，20 feedback age，
24 电压mV，28 mm/s，32 mrad/s，36/40 指令目标，44/46 左右RPM，48 work mode，49 disabled，
52/56/60 控制转发/队列丢弃/反馈转发计数。
阿克曼目标为 mm/s 和转角 mrad；履带目标为左右有符号百分比，不是车速或角速度。
未知 age=UINT32_MAX，未知 mode/disabled=255。无 SOC、无输出使能推断、无伪造在线。
诊断回复使用非阻塞、分批 UART 发送，避免一次回复阻塞 CAN 转发。

## 验收边界

主机字节/队列/解析测试、官方 SDK 交叉编译已可执行；没有烧录实板，不等于硬件验收。
待现场验证：两侧接线、终端电阻、反馈稳定性、遥控接管、急停、断链停车和总线错误恢复。
