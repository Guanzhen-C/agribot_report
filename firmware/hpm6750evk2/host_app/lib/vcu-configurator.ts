import {
  BUILTIN_COMPUTE_INTERFACES,
  BUILTIN_MACHINES,
  BUILTIN_PROTOCOLS,
  ComputeInterfaceDefinition,
  MachineDefinition,
  ProtocolDefinition,
} from "./config-catalog";
import type {
  ConfigTransport,
  ConfigTransportFactory,
  TransportConnectionInfo,
} from "./config-transport";
import {
  ACK_PAYLOAD_SIZE,
  ACK_STATUS,
  decodeWriteSelectionAck,
  encodeWriteSelection,
  FRAME_CRC_SIZE,
  FRAME_HEADER_SIZE,
  toHex,
} from "./serial-frame.mjs";
import { WebSerialConfigTransport } from "./web-serial-transport";

export const CONFIGURATOR_API_VERSION = "1.1";

export interface Selection {
  machineKey: string;
  protocolKey: string;
  computeInterfaceKey: string;
}

export interface SelectionPreview {
  machine: MachineDefinition;
  protocol: ProtocolDefinition;
  computeInterface: ComputeInterfaceDefinition;
  frame: Uint8Array;
  frameHex: string;
  sequence: number;
}

export interface WriteSelectionResult {
  machine: MachineDefinition;
  protocol: ProtocolDefinition;
  computeInterface: ComputeInterfaceDefinition;
  generation: number;
  activeSlot: "A" | "B";
  sequence: number;
}

export type ConfiguratorEvent = "connection" | "write" | "error";
export type ConfiguratorListener = (value: unknown) => void;

const STATUS_MESSAGES: Record<number, string> = {
  [ACK_STATUS.BAD_FRAME]: "VCU 拒绝了损坏的配置帧",
  [ACK_STATUS.UNSUPPORTED_VERSION]: "VCU 不支持当前配置帧版本",
  [ACK_STATUS.UNSUPPORTED_MACHINE]: "VCU 固件不支持所选农机",
  [ACK_STATUS.UNSUPPORTED_PROTOCOL]: "VCU 固件不支持所选协议",
  [ACK_STATUS.INCOMPATIBLE_SELECTION]: "所选农机与协议不兼容",
  [ACK_STATUS.VCU_NOT_STANDBY]: "VCU 未处于停车待机状态",
  [ACK_STATUS.FLASH_WRITE_FAILED]: "VCU 写入 Flash 失败",
  [ACK_STATUS.VERIFY_FAILED]: "VCU 写入后校验失败",
  [ACK_STATUS.UNSUPPORTED_COMPUTE_INTERFACE]: "VCU 固件不支持所选算力接口",
};

export class VcuConfigurator {
  readonly apiVersion = CONFIGURATOR_API_VERSION;
  private readonly machines = new Map<string, MachineDefinition>();
  private readonly protocols = new Map<string, ProtocolDefinition>();
  private readonly computeInterfaces = new Map<string, ComputeInterfaceDefinition>();
  private readonly transports = new Map<string, ConfigTransportFactory>();
  private readonly listeners = new Map<ConfiguratorEvent, Set<ConfiguratorListener>>();
  private transport: ConfigTransport | null = null;
  private connection: TransportConnectionInfo | null = null;
  private nextSequence = 1;
  private writing = false;
  private removeDisconnectListener: (() => void) | undefined;

  constructor() {
    BUILTIN_MACHINES.forEach((machine) => this.registerMachine(machine));
    BUILTIN_PROTOCOLS.forEach((protocol) => this.registerProtocol(protocol));
    BUILTIN_COMPUTE_INTERFACES.forEach((definition) =>
      this.registerComputeInterface(definition));
    this.registerTransport("web-serial", () => new WebSerialConfigTransport());
  }

  registerMachine(machine: MachineDefinition) {
    if (this.machines.has(machine.key)) throw new Error(`农机已注册：${machine.key}`);
    this.machines.set(machine.key, { ...machine });
  }

  registerProtocol(protocol: ProtocolDefinition) {
    if (this.protocols.has(protocol.key)) throw new Error(`协议已注册：${protocol.key}`);
    this.protocols.set(protocol.key, {
      ...protocol,
      compatibleMachineKeys: [...protocol.compatibleMachineKeys],
    });
  }

  registerComputeInterface(definition: ComputeInterfaceDefinition) {
    if (this.computeInterfaces.has(definition.key)) {
      throw new Error(`算力接口已注册：${definition.key}`);
    }
    if (Array.from(this.computeInterfaces.values()).some((item) => item.id === definition.id)) {
      throw new Error(`算力接口 ID 已注册：${definition.id}`);
    }
    this.computeInterfaces.set(definition.key, { ...definition });
  }

  registerTransport(key: string, factory: ConfigTransportFactory) {
    if (this.transports.has(key)) throw new Error(`传输适配器已注册：${key}`);
    this.transports.set(key, factory);
  }

  listMachines() {
    return Array.from(this.machines.values(), (machine) => ({ ...machine }));
  }

  listProtocols(machineKey?: string) {
    return Array.from(this.protocols.values())
      .filter(
        (protocol) =>
          !machineKey || protocol.compatibleMachineKeys.includes(machineKey),
      )
      .map((protocol) => ({
        ...protocol,
        compatibleMachineKeys: [...protocol.compatibleMachineKeys],
      }));
  }

  listComputeInterfaces() {
    return Array.from(this.computeInterfaces.values(), (definition) => ({
      ...definition,
    }));
  }

  listTransports() {
    return Array.from(this.transports, ([key, factory]) => {
      const transport = factory();
      return { key, label: transport.label, supported: transport.isSupported() };
    });
  }

  getConnection() {
    return this.connection ? { ...this.connection } : null;
  }

  async connect(options: { transport?: string; baudRate?: number } = {}) {
    await this.disconnect();
    const transportKey = options.transport ?? "web-serial";
    const factory = this.transports.get(transportKey);
    if (!factory) throw new Error(`未注册传输适配器：${transportKey}`);
    const transport = factory();
    if (!transport.isSupported()) throw new Error(`${transport.label} 在当前环境不可用`);
    try {
      this.connection = await transport.open({ baudRate: options.baudRate ?? 115200 });
      this.transport = transport;
      this.removeDisconnectListener = transport.onDisconnect?.(() => {
        this.removeDisconnectListener?.();
        this.removeDisconnectListener = undefined;
        this.transport = null;
        this.connection = null;
        this.emit("connection", null);
        void transport.close().catch(() => undefined);
      });
      this.emit("connection", this.connection);
      return { ...this.connection };
    } catch (error) {
      this.emit("error", error);
      throw error;
    }
  }

  async disconnect() {
    if (this.writing) throw new Error("正在保存，请稍候");
    const transport = this.transport;
    this.removeDisconnectListener?.();
    this.removeDisconnectListener = undefined;
    this.transport = null;
    this.connection = null;
    this.emit("connection", null);
    if (transport) await transport.close();
  }

  previewSelection(selection: Selection): SelectionPreview {
    const { machine, protocol, computeInterface } = this.resolveSelection(selection);
    const sequence = this.nextSequence >>> 0;
    const frame = encodeWriteSelection({
      machineId: machine.id,
      protocolId: protocol.id,
      computeInterfaceId: computeInterface.id,
      sequence,
    });
    return { machine, protocol, computeInterface, sequence, frame, frameHex: toHex(frame) };
  }

  async writeSelection(selection: Selection): Promise<WriteSelectionResult> {
    if (this.writing) throw new Error("正在保存，请稍候");
    if (!this.transport || !this.connection) throw new Error("请先连接 VCU 串口");
    const { machine, protocol, computeInterface } = this.resolveSelection(selection);
    const sequence = this.nextSequence >>> 0;
    this.nextSequence = (this.nextSequence + 1) >>> 0;
    const frame = encodeWriteSelection({
      machineId: machine.id,
      protocolId: protocol.id,
      computeInterfaceId: computeInterface.id,
      sequence,
    });

    this.writing = true;
    const transport = this.transport;
    try {
      const ackBytes = await transport.exchange(
        frame,
        FRAME_HEADER_SIZE + ACK_PAYLOAD_SIZE + FRAME_CRC_SIZE,
        1500,
      );
      const ack = decodeWriteSelectionAck(ackBytes, sequence);
      if (this.transport !== transport) throw new Error("设备已断开连接");
      if (ack.status !== ACK_STATUS.OK) {
        throw new Error(STATUS_MESSAGES[ack.status] ?? `VCU 返回未知错误：${ack.status}`);
      }
      if (ack.machineId !== machine.id || ack.protocolId !== protocol.id) {
        throw new Error("VCU 确认的农机或协议与写入请求不一致");
      }
      if (ack.activeSlot !== 0 && ack.activeSlot !== 1) throw new Error("确认数据校验失败");
      const result: WriteSelectionResult = {
        machine,
        protocol,
        computeInterface,
        generation: ack.generation,
        activeSlot: ack.activeSlot === 0 ? "A" : "B",
        sequence,
      };
      this.emit("write", result);
      return result;
    } catch (error) {
      this.writing = false;
      await this.disconnect().catch(() => undefined);
      this.emit("error", error);
      throw error;
    } finally {
      this.writing = false;
    }
  }

  on(event: ConfiguratorEvent, listener: ConfiguratorListener) {
    const listeners = this.listeners.get(event) ?? new Set<ConfiguratorListener>();
    listeners.add(listener);
    this.listeners.set(event, listeners);
    return () => { listeners.delete(listener); };
  }

  private resolveSelection(selection: Selection) {
    const machine = this.machines.get(selection.machineKey);
    const protocol = this.protocols.get(selection.protocolKey);
    const computeInterface = this.computeInterfaces.get(selection.computeInterfaceKey);
    if (!machine) throw new Error(`未知农机：${selection.machineKey}`);
    if (!protocol) throw new Error(`未知协议：${selection.protocolKey}`);
    if (!computeInterface) throw new Error(`未知算力接口：${selection.computeInterfaceKey}`);
    if (!protocol.compatibleMachineKeys.includes(machine.key)) {
      throw new Error(`${protocol.label} 不适用于 ${machine.label}`);
    }
    return { machine, protocol, computeInterface };
  }

  private emit(event: ConfiguratorEvent, value: unknown) {
    this.listeners.get(event)?.forEach((listener) => listener(value));
  }
}

export const fieldGateConfigurator = new VcuConfigurator();

declare global {
  interface Window {
    fieldGateConfigurator?: VcuConfigurator;
  }
}

export function exposeConfiguratorApi() {
  if (typeof window !== "undefined") {
    window.fieldGateConfigurator = fieldGateConfigurator;
  }
  return fieldGateConfigurator;
}
