import { crc32 } from "./serial-frame.mjs";

export const TELEMETRY_FRAME_SIZE = 80;
export function encodeTelemetryQuery(sequence) {
  const bytes = new Uint8Array(24), view = new DataView(bytes.buffer);
  bytes.set([0x56, 0x43, 0x46, 0x47, 1, 0x20, 8, 0]);
  view.setUint32(8, sequence, true); view.setUint32(20, crc32(bytes.subarray(0, 20)), true);
  return bytes;
}

export function decodeTelemetry(bytes, sequence) {
  if (bytes.length === 24 && bytes[5] === 0x90) throw new Error("TELEMETRY_UNSUPPORTED");
  if (bytes.length !== TELEMETRY_FRAME_SIZE) throw new Error("TELEMETRY_BAD_LENGTH");
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  if (view.getUint32(0, false) !== 0x56434647 || bytes[4] !== 1 || bytes[5] !== 0xa0
    || view.getUint16(6, true) !== 64 || ![1, 2].includes(bytes[12])) throw new Error("TELEMETRY_BAD_HEADER");
  if (view.getUint32(76, true) !== crc32(bytes.subarray(0, 76))) throw new Error("TELEMETRY_BAD_CRC");
  if (view.getUint32(8, true) !== (sequence >>> 0)) throw new Error("TELEMETRY_BAD_SEQUENCE");
  const flags = bytes[13], valid = view.getUint16(22, true);
  if ((flags & ~31) || (valid & ~15) || bytes[20] > 2) throw new Error("TELEMETRY_BAD_VALUE");
  const feedbackFresh = Boolean(flags & 4);
  const inputFresh = Boolean(flags & 2);
  const age = offset => { const value = view.getUint32(offset, true); return value === 0xffffffff ? null : value; };
  if (bytes[12] === 2) {
    const machineId = view.getUint16(16, true);
    if (![1, 0x12].includes(machineId)) throw new Error("TELEMETRY_BAD_PROFILE");
    return {
      schema: 2, configured: Boolean(flags & 1), computeOnline: inputFresh, actuatorOnline: feedbackFresh,
      hardwareReady: Boolean(flags & 8), outputEnabled: null,
      fault: view.getUint16(14, true), machineId, protocolId: view.getUint16(18, true),
      computeInterfaceId: bytes[20], uptimeMs: view.getUint32(24, true), commandAgeMs: age(28), feedbackAgeMs: age(32),
      voltage: (valid & 1) ? view.getInt32(36, true) / 1000 : null,
      speed: (valid & 2) ? view.getInt32(40, true) / 1000 : null,
      yawRate: (valid & 2) ? view.getInt32(44, true) / 1000 : null,
      targetSpeed: inputFresh && machineId === 0x12 ? view.getInt32(48, true) / 1000 : null,
      targetSteeringAngle: inputFresh && machineId === 0x12 ? view.getInt32(52, true) / 1000 : null,
      targetYawRate: null,
      targetWheelPercent: inputFresh && machineId === 1 ? [view.getInt32(48, true), view.getInt32(52, true)] : null,
      leftRpm: (valid & 4) ? view.getInt16(56, true) : null,
      rightRpm: (valid & 8) ? view.getInt16(58, true) : null,
      workMode: bytes[60] === 255 ? null : bytes[60],
      driveDisabled: bytes[61] === 255 ? null : Boolean(bytes[61]), batteryPercent: null, requested: null,
      functions: { fan: null, pump: null, shredder: null, headlight: null },
      acceptedFrames: view.getUint32(64, true), rejectedFrames: view.getUint32(68, true),
      feedbackFrames: view.getUint32(72, true),
    };
  }
  const soc = view.getUint16(56, true);
  if ((valid & 4) && soc > 10000) throw new Error("TELEMETRY_BAD_SOC");
  return {
    configured: Boolean(flags & 1), computeOnline: inputFresh, actuatorOnline: feedbackFresh,
    hardwareReady: Boolean(flags & 8), outputEnabled: Boolean(flags & 16),
    fault: view.getUint16(14, true), machineId: view.getUint16(16, true), protocolId: view.getUint16(18, true),
    computeInterfaceId: bytes[20], uptimeMs: view.getUint32(24, true),
    commandAgeMs: age(28), feedbackAgeMs: age(32),
    voltage: feedbackFresh && (valid & 1) ? view.getInt32(36, true) / 1000 : null,
    speed: feedbackFresh && (valid & 2) ? view.getInt32(40, true) / 1000 : null,
    yawRate: feedbackFresh && (valid & 2) ? view.getInt32(44, true) / 1000 : null,
    targetSpeed: inputFresh ? view.getInt32(48, true) / 1000 : null,
    targetYawRate: inputFresh ? view.getInt32(52, true) / 1000 : null,
    batteryPercent: feedbackFresh && (valid & 4) ? soc / 100 : null,
    driveDisabled: feedbackFresh && (valid & 2) ? Boolean(bytes[61]) : null,
    requested: inputFresh ? { fan: bytes[58], pump: bytes[59], shredder: view.getUint16(62, true) / 100,
      headlight: Boolean(bytes[60] & 4), enabled: Boolean(bytes[60] & 1), stop: Boolean(bytes[60] & 2) } : null,
    // A request is not device feedback. v1 has no work-device status payload.
    functions: { fan: null, pump: null, shredder: null, headlight: null },
    acceptedFrames: view.getUint32(64, true), rejectedFrames: view.getUint32(68, true),
  };
}

export function encodeComputeSerial(command) {
  const { speed = 0, yawRate = 0, enabled = false, stop = false, headlight = false,
    fan = 0, pump = 0, shredder = 0, sequence = 0, sourceTimeMs = 0 } = command;
  if (![speed, yawRate, shredder].every(Number.isFinite) || Math.abs(speed) > 32.767 || Math.abs(yawRate) > 32.767
    || !Number.isInteger(fan) || fan < 0 || fan > 8 || !Number.isInteger(pump) || pump < 0 || pump > 8
    || shredder < 0 || shredder > 100) throw new RangeError("COMMAND_OUT_OF_RANGE");
  const bytes = new Uint8Array(32), view = new DataView(bytes.buffer);
  bytes.set([0x46, 0x47, 1, 0x10, 32, 0]); view.setUint16(6, sequence, true); view.setUint32(8, sourceTimeMs, true);
  view.setInt32(12, Math.round(speed * 1000), true); view.setInt32(16, Math.round(yawRate * 1000), true);
  view.setUint16(20, (enabled ? 1 : 0) | (stop ? 2 : 0) | (headlight ? 4 : 0), true);
  bytes[22] = fan; bytes[23] = pump; view.setUint16(24, Math.round(shredder * 100), true);
  view.setUint16(26, 300, true); view.setUint32(28, crc32(bytes.subarray(0, 28)), true); return bytes;
}
