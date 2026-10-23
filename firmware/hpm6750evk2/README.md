# HPM6750EVK2 macOS 开发环境

加载仓库内的 SDK、RISC-V 工具链、Python 环境和 HPM OpenOCD：

```sh
source ./env.sh
```

编译 SDK 的 `hello_world` RAM 镜像：

```sh
cmake -S "$HPM_SDK_BASE/samples/hello_world" \
    -B build/hello_world \
    -GNinja \
    -DBOARD=hpm6750evk2 \
    -DCMAKE_BUILD_TYPE=debug \
    -DHPM_BUILD_TYPE=ram \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5
cmake --build build/hello_world
```

通过 fireDAP 下载到 RAM 并运行，不改写板上 Flash：

```sh
hpm_openocd \
    -f probes/cmsis_dap.cfg \
    -c "adapter speed $HPM_JTAG_SPEED_KHZ" \
    -f soc/hpm6750-single-core.cfg \
    -f boards/hpm6750evk2.cfg \
    -c init \
    -c "reset halt" \
    -c "load_image build/hello_world/output/demo.elf" \
    -c "verify_image build/hello_world/output/demo.elf" \
    -c "reg pc 0" \
    -c resume \
    -c shutdown
```

`reset halt` 会先清理上一个 RAM 程序留下的外设和中断状态；OpenOCD 此时可能打印
`Hart unexpectedly reset!`，只要随后出现下载和校验成功信息即可。

调试时，第一个终端启动 OpenOCD：

```sh
hpm_openocd \
    -f probes/cmsis_dap.cfg \
    -c "adapter speed $HPM_JTAG_SPEED_KHZ" \
    -f soc/hpm6750-single-core.cfg \
    -f boards/hpm6750evk2.cfg
```

第二个终端启动 GDB：

```sh
source ./env.sh
riscv32-unknown-elf-gdb build/hello_world/output/demo.elf
```

```gdb
target extended-remote localhost:3333
load
break main
set $pc = 0
continue
```

fireDAP 串口通常为 `/dev/cu.usbmodem*`，本机当前是
`/dev/cu.usbmodem21301`；串口参数为 115200、8N1。

批量配置并编译 SDK 中适用于本板卡的全部 RAM Debug 例程：

```sh
python3 scripts/build_all_samples.py
```

详细日志和汇总写入 `build/test-results/all-samples/`。SDK 明确标记为
板卡不支持、排除 RAM Debug 构建，或要求 Andes/ZCC DSP 工具链的例程会记为
`skipped`，不会记为编译失败。

## VCU 上位机

`host_app/` 中提供了串口配置上位机，用于选择算力输入接口、农机和 CAN 协议并
持久化写入 VCU：

```sh
cd host_app
npm install
npm run dev
```

详细说明见 `host_app/README.md`，程序 API、配置协议与算力运行协议分别见
`host_app/docs/PROGRAM_API.md`、`host_app/docs/SERIAL_CONFIG_PROTOCOL.md` 和
`host_app/docs/RUNTIME_COMMAND_PROTOCOL.md`。

## VCU 配置固件

`firmware/vcu_configurator/` 是与上位机配套的 HPM6750 固件，使用 UART0 接收
`VCFG/1`，并把算力接口、农机和协议选择以 A/B 双槽和 CRC32 持久化到 QSPI NOR
最后两个扇区。CAN/SocketCAN 模式使用扩展口 CAN1 和外接收发器，板载 CAN0 接
农机。构建、RAM 下载、Mac 串口测试和 Flash 后端自测方法见
`firmware/vcu_configurator/README.md`。
