import type {
  ConfigTransport,
  TransportConnectionInfo,
  TransportOpenOptions,
} from "./config-transport";

interface SerialPortInfo {
  usbVendorId?: number;
  usbProductId?: number;
}

interface BrowserSerialPort {
  readable: ReadableStream<Uint8Array> | null;
  writable: WritableStream<Uint8Array> | null;
  open(options: { baudRate: number; dataBits: 8; stopBits: 1; parity: "none" }): Promise<void>;
  close(): Promise<void>;
  getInfo(): SerialPortInfo;
  addEventListener(type: "disconnect", listener: () => void): void;
  removeEventListener(type: "disconnect", listener: () => void): void;
}

interface BrowserSerialApi {
  requestPort(options?: { filters?: Array<Record<string, number>> }): Promise<BrowserSerialPort>;
}

declare global {
  interface Navigator {
    serial?: BrowserSerialApi;
  }
}

function appendBytes(left: Uint8Array, right: Uint8Array) {
  const combined = new Uint8Array(left.length + right.length);
  combined.set(left);
  combined.set(right, left.length);
  return combined;
}

export class WebSerialConfigTransport implements ConfigTransport {
  readonly key = "web-serial";
  readonly label = "浏览器串口";
  private port: BrowserSerialPort | null = null;
  private baudRate = 115200;

  isSupported() {
    return typeof navigator !== "undefined" && Boolean(navigator.serial);
  }

  async open(options: TransportOpenOptions): Promise<TransportConnectionInfo> {
    if (!navigator.serial) {
      throw new Error("当前浏览器不支持 Web Serial，请使用最新版 Chrome 或 Edge");
    }
    if (this.port) await this.close();
    const port = await navigator.serial.requestPort();
    await port.open({
      baudRate: options.baudRate,
      dataBits: 8,
      stopBits: 1,
      parity: "none",
    });
    this.port = port;
    this.baudRate = options.baudRate;
    const info = port.getInfo();
    return {
      transport: this.key,
      label: this.label,
      baudRate: this.baudRate,
      usbVendorId: info.usbVendorId,
      usbProductId: info.usbProductId,
    };
  }

  async close() {
    if (!this.port) return;
    const port = this.port;
    this.port = null;
    await port.close();
  }

  onDisconnect(listener: () => void) {
    const port = this.port;
    port?.addEventListener("disconnect", listener);
    return () => port?.removeEventListener("disconnect", listener);
  }

  async exchange(frame: Uint8Array, expectedLength: number, timeoutMs: number) {
    const port = this.port;
    if (!port?.writable || !port.readable) throw new Error("串口尚未连接");

    const writer = port.writable.getWriter();
    try {
      await writer.write(frame);
    } finally {
      writer.releaseLock();
    }

    const reader = port.readable.getReader();
    let received = new Uint8Array();
    let timeoutHandle: ReturnType<typeof setTimeout> | undefined;
    const deadline = performance.now() + timeoutMs;
    try {
      while (received.length < expectedLength) {
        const remaining = new Promise<never>((_, reject) => {
          timeoutHandle = setTimeout(
            () => reject(new Error("等待 VCU 确认超时")),
            Math.max(0, deadline - performance.now()),
          );
        });
        const result = await Promise.race([reader.read(), remaining]);
        if (timeoutHandle) clearTimeout(timeoutHandle);
        if (result.done) throw new Error("VCU 在返回确认前关闭了串口");
        if (result.value) received = appendBytes(received, result.value);
      }
      return received.subarray(0, expectedLength);
    } catch (error) {
      await reader.cancel().catch(() => undefined);
      throw error;
    } finally {
      if (timeoutHandle) clearTimeout(timeoutHandle);
      reader.releaseLock();
    }
  }
}
