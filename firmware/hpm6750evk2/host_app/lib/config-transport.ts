export interface TransportOpenOptions {
  baudRate: number;
}

export interface TransportConnectionInfo {
  transport: string;
  label: string;
  baudRate: number;
  usbVendorId?: number;
  usbProductId?: number;
}

export interface ConfigTransport {
  readonly key: string;
  readonly label: string;
  isSupported(): boolean;
  open(options: TransportOpenOptions): Promise<TransportConnectionInfo>;
  close(): Promise<void>;
  onDisconnect?(listener: () => void): () => void;
  exchange(frame: Uint8Array, expectedLength: number, timeoutMs: number): Promise<Uint8Array>;
}

export type ConfigTransportFactory = () => ConfigTransport;
