// 禾川运动协议：多字节数据为大端，保持已有设备的线上格式。
export const HECHUAN_CAN_BITRATE = 1_000_000;
export const HECHUAN_SERIAL_BAUD_RATE = 115200;
export const HECHUAN_COMMAND_ID = 0x181;
export const HECHUAN_FEEDBACK_IDS = Object.freeze([0x101, 0x102, 0x103]);
export const HECHUAN_FEEDBACK_SIZE = 24;

export function xorChecksum(bytes) {
  return bytes.reduce((sum, byte) => sum ^ byte, 0);
}

function scaledInt16(value) {
  if (!Number.isFinite(value)) throw new RangeError("MOTION_VALUE_NOT_FINITE");
  const scaled = Math.round(value * 1000);
  if (scaled < -32768 || scaled > 32767) throw new RangeError("MOTION_VALUE_OUT_OF_RANGE");
  return scaled;
}

function writeMotion(view, offset, forward, lateral, turn) {
  view.setInt16(offset, scaledInt16(forward), false);
  view.setInt16(offset + 2, scaledInt16(lateral), false);
  view.setInt16(offset + 4, scaledInt16(turn), false);
}

export function encodeSerialMotion({ forward = 0, lateral = 0, yawRate = 0 } = {}) {
  const bytes = new Uint8Array(11);
  bytes[0] = 0x7b;
  // Byte1=0 为常规运行，Byte2 保留；不自动开启其他工作模式。
  writeMotion(new DataView(bytes.buffer), 3, forward, lateral, yawRate);
  bytes[9] = xorChecksum(bytes.subarray(0, 9));
  bytes[10] = 0x7d;
  return bytes;
}

export function decodeSerialMotion(bytes) {
  if (bytes.length !== 11 || bytes[0] !== 0x7b || bytes[10] !== 0x7d
      || xorChecksum(bytes.subarray(0, 9)) !== bytes[9]) throw new Error("MOTION_BAD_FRAME");
  if (bytes[1] !== 0 || bytes[2] !== 0) throw new Error("MOTION_UNSUPPORTED_MODE");
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  return { forward: view.getInt16(3, false) / 1000, lateral: view.getInt16(5, false) / 1000,
    yawRate: view.getInt16(7, false) / 1000 };
}

function canMotion(forward, lateral, turn) {
  const data = new Uint8Array(8);
  writeMotion(new DataView(data.buffer), 0, forward, lateral, turn);
  return { id: HECHUAN_COMMAND_ID, extended: false, data };
}

export function encodeVelocityCanMotion({ forward = 0, lateral = 0, yawRate = 0 } = {}) {
  return canMotion(forward, lateral, yawRate);
}

export function encodeSteeringCanMotion({ forward = 0, steeringAngle = 0 } = {}) {
  // CAN 转向型号接受左前轮角度（rad）；不能把 yawRate 原样填入。
  return canMotion(forward, 0, steeringAngle);
}

export function decodeMotionFeedback(bytes) {
  if (bytes.length !== HECHUAN_FEEDBACK_SIZE || bytes[0] !== 0x7b || bytes[23] !== 0x7d
      || xorChecksum(bytes.subarray(0, 22)) !== bytes[22]) throw new Error("FEEDBACK_BAD_FRAME");
  if (bytes[1] > 1) throw new Error("FEEDBACK_BAD_STATE");
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const triple = (offset) => [0, 2, 4].map((delta) => view.getInt16(offset + delta, false));
  return {
    driveDisabled: bytes[1] === 1,
    forward: view.getInt16(2, false) / 1000,
    lateral: view.getInt16(4, false) / 1000,
    yawRate: view.getInt16(6, false) / 1000,
    accelerationRaw: triple(8),
    angularVelocityRaw: triple(14),
    voltage: view.getInt16(20, false) / 1000,
  };
}

export class MotionFeedbackStream {
  buffer = new Uint8Array();
  feed(chunk) {
    const joined = new Uint8Array(this.buffer.length + chunk.length);
    joined.set(this.buffer); joined.set(chunk, this.buffer.length);
    const results = [];
    let offset = 0;
    while (offset < joined.length) {
      if (joined[offset] !== 0x7b) { offset++; continue; }
      if (joined.length - offset < HECHUAN_FEEDBACK_SIZE) break;
      try {
        results.push(decodeMotionFeedback(joined.subarray(offset, offset + HECHUAN_FEEDBACK_SIZE)));
        offset += HECHUAN_FEEDBACK_SIZE;
      } catch { offset++; }
    }
    this.buffer = joined.slice(offset);
    return results;
  }
}

export class MotionCanFeedback {
  parts = [];
  startedAt = 0;
  lastAt = 0;
  feed({ id, extended = false, remote = false, data }, nowMs) {
    if (!HECHUAN_FEEDBACK_IDS.includes(id)) return null;
    if (!Number.isFinite(nowMs) || nowMs < this.lastAt || extended || remote || data.length !== 8) {
      this.parts = []; return null;
    }
    this.lastAt = nowMs;
    if (id === HECHUAN_FEEDBACK_IDS[0]) { this.parts = [data.slice()]; this.startedAt = nowMs; return null; }
    if (nowMs - this.startedAt > 100 || id !== HECHUAN_FEEDBACK_IDS[this.parts.length]) {
      this.parts = []; return null;
    }
    this.parts.push(data.slice());
    if (this.parts.length !== 3) return null;
    const bytes = new Uint8Array(this.parts.flatMap((part) => Array.from(part)));
    this.parts = [];
    return decodeMotionFeedback(bytes);
  }
}
