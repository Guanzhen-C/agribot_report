# FieldGate VCU 配置器程序 API

版本：1.1

这里的 API 是上位机程序暴露给界面、插件和后续功能模块的代码接口，不是
HTTP 或网络接口。浏览器页面加载后可通过：

```js
window.fieldGateConfigurator
```

访问同一个 `VcuConfigurator` 实例。TypeScript 模块可以直接导入：

```ts
import { fieldGateConfigurator } from "../lib/vcu-configurator";
```

## 最小写入流程

```js
const api = window.fieldGateConfigurator;

await api.connect({
  transport: "web-serial",
  baudRate: 115200,
});

const machines = api.listMachines();
const protocols = api.listProtocols(machines[0].key);
const computeInterfaces = api.listComputeInterfaces();

const result = await api.writeSelection({
  machineKey: machines[0].key,
  protocolKey: protocols[0].key,
  computeInterfaceKey: computeInterfaces[0].key,
});

console.log(result.generation, result.activeSlot);
```

`writeSelection()` 只有在以下条件全部满足时才成功返回：

1. 配置帧已经完整写入串口；
2. VCU 返回相同序号的 ACK；
3. ACK 的 CRC32 正确；
4. ACK 中的农机 ID、协议 ID 与请求一致；
5. VCU 报告 Flash 写入和回读校验成功。

`computeInterfaceKey` 的内置选择为 `socketcan_can1`、`raw_can1` 和
`serial_uart13`。其中 CAN 与 SocketCAN 在 VCU 端都使用 CAN1，区别仅是算力平台
使用的程序接口。

## API 方法

### `connect(options?): Promise<TransportConnectionInfo>`

打开配置传输。内置 `web-serial` 适配器会弹出浏览器串口选择窗口。

```ts
await api.connect({ transport: "web-serial", baudRate: 115200 });
```

### `disconnect(): Promise<void>`

释放串口及读写锁。

### `getConnection(): TransportConnectionInfo | null`

返回当前连接信息，不读取 VCU 实时状态。

### `listMachines(): MachineDefinition[]`

返回程序已注册的农机类型。内置定义：

| key | ID | 名称 |
| --- | --- | --- |
| `tracked_three_in_one` | `0x0001` | 履带差速农机（三合一） |

### `listProtocols(machineKey?): ProtocolDefinition[]`

返回全部协议或指定农机兼容的协议。内置定义：

| key | ID | CAN 波特率 | 名称 |
| --- | --- | --- | --- |
| `three_in_one_can_v1` | `0x0101` | 250000 | 三合一 CAN 协议 2026-08-26/v1 |

### `listComputeInterfaces(): ComputeInterfaceDefinition[]`

返回允许持久化写入 VCU 的算力输入接口：

| key | ID | VCU 端口 | 算力侧 |
| --- | ---: | --- | --- |
| `socketcan_can1` | `0x02` | CAN1 / 500kbps | Linux SocketCAN `can0` |
| `raw_can1` | `0x01` | CAN1 / 500kbps | 厂商或原始 CAN API |
| `serial_uart13` | `0x00` | UART13 / 115200 | 3.3V TTL 串口 |

### `previewSelection(selection): SelectionPreview`

只生成配置帧，不操作串口。可用于日志、测试和界面预览。

### `writeSelection(selection): Promise<WriteSelectionResult>`

写入农机/协议配置并等待 VCU ACK。成功结果包含：

```ts
{
  machine: MachineDefinition;
  protocol: ProtocolDefinition;
  computeInterface: ComputeInterfaceDefinition;
  generation: number;
  activeSlot: "A" | "B";
  sequence: number;
}
```

### `registerMachine(definition): void`

注册新农机。`key` 和 `id` 都应在整个产品中保持稳定。

```ts
api.registerMachine({
  key: "orchard_ackermann",
  id: 0x0002,
  label: "果园阿克曼农机",
  description: "前轮转向平台",
});
```

### `registerProtocol(definition): void`

注册新协议，并声明允许使用它的农机。

```ts
api.registerProtocol({
  key: "orchard_can_v1",
  id: 0x0201,
  label: "果园车 CAN 协议",
  version: "v1",
  canBitrate: 500000,
  compatibleMachineKeys: ["orchard_ackermann"],
});
```

注册表决定上位机允许写入的组合；VCU 固件仍须维护同一份 ID 映射并独立检查。

### `registerComputeInterface(definition): void`

注册新的算力输入适配方式。ID 必须与 VCU 固件一致，不能复用已发布 ID。

### `registerTransport(key, factory): void`

注册新的串口或本地硬件适配器。传输适配器只负责传输完整二进制帧，不理解
农机和 CAN 协议。

### `on(event, listener): () => void`

事件包括 `connection`、`write`、`error`。返回值用于取消订阅。这些是配置事务
事件，不是车辆实时遥测。

## 文件职责

| 文件 | 职责 |
| --- | --- |
| `lib/vcu-configurator.ts` | 对外程序 API、注册表、写入事务 |
| `lib/config-catalog.ts` | 内置农机和协议 ID |
| `lib/config-transport.ts` | 可插拔传输接口 |
| `lib/web-serial-transport.ts` | 浏览器 Web Serial 实现 |
| `lib/serial-frame.mjs` | VCFG 帧、CRC32、ACK 编解码 |

后续界面、批量配置、生产写号或自动化测试都应调用 `VcuConfigurator`，不要直接
操作 Web Serial，这样更换 Electron、Node 串口或原生 macOS 后端时无需修改业务
逻辑。
