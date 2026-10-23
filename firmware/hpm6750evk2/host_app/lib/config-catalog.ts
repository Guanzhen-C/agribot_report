export interface MachineDefinition {
  key: string;
  id: number;
  label: string;
  description: string;
}

export interface ProtocolDefinition {
  key: string;
  id: number;
  label: string;
  version: string;
  canBitrate: number;
  compatibleMachineKeys: string[];
}

export interface ComputeInterfaceDefinition {
  key: string;
  id: number;
  label: string;
  description: string;
  physicalPort: "CAN1" | "UART13";
  transport: "can" | "socketcan" | "serial";
  bitrate: number;
}

export const BUILTIN_MACHINES: MachineDefinition[] = [
  { key: "tracked_three_in_one", id: 0x0001, label: "履带差速农机（三合一）", description: "左右履带独立驱动，可扩展风机、水泵和粉碎机。" },
  { key: "hechuan_differential", id: 0x0010, label: "差速农机", description: "两侧独立驱动，差速转向。" },
  { key: "hechuan_four_wheel", id: 0x0011, label: "四驱农机", description: "四轮驱动，两侧差速转向。" },
  { key: "hechuan_steering", id: 0x0012, label: "转向农机", description: "前轮转向，后轮驱动。" },
  { key: "hechuan_mecanum", id: 0x0013, label: "横移农机", description: "四轮独立驱动，支持横向移动。" },
  { key: "hechuan_omni", id: 0x0014, label: "全向农机", description: "三轮全向驱动，支持平移与转向。" },
];

export const BUILTIN_PROTOCOLS: ProtocolDefinition[] = [
  {
    key: "hechuan_motion_v1", id: 0x0201, label: "通用运动协议",
    version: "2026-05-29 / v1", canBitrate: 1000000,
    compatibleMachineKeys: ["hechuan_differential", "hechuan_four_wheel", "hechuan_mecanum", "hechuan_omni"],
  },
  {
    key: "hechuan_steering_v1", id: 0x0202, label: "转向运动协议",
    version: "2026-05-29 / v1", canBitrate: 1000000,
    compatibleMachineKeys: ["hechuan_steering"],
  },
  {
    key: "three_in_one_can_v1", id: 0x0101, label: "三合一 CAN 协议",
    version: "2026-08-26 / v1", canBitrate: 250000,
    compatibleMachineKeys: ["tracked_three_in_one"],
  },
];

export const BUILTIN_COMPUTE_INTERFACES: ComputeInterfaceDefinition[] = [
  {
    key: "socketcan_can1", id: 0x02, label: "SocketCAN",
    description: "适用于提供 SocketCAN 接口的算力模块。",
    physicalPort: "CAN1", transport: "socketcan", bitrate: 500000,
  },
  {
    key: "raw_can1", id: 0x01, label: "标准 CAN",
    description: "适用于直接发送控制报文的算力模块。",
    physicalPort: "CAN1", transport: "can", bitrate: 500000,
  },
  {
    key: "serial_uart13", id: 0x00, label: "串行连接",
    description: "适用于使用串行线缆的控制设备。",
    physicalPort: "UART13", transport: "serial", bitrate: 115200,
  },
];
