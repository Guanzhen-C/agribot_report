export const FRAME_VERSION = 1;
export const WRITE_SELECTION_OPCODE = 0x10;
export const WRITE_SELECTION_ACK_OPCODE = 0x90;
export const FRAME_HEADER_SIZE = 12;
export const FRAME_CRC_SIZE = 4;
export const SELECTION_PAYLOAD_SIZE = 8;
export const ACK_PAYLOAD_SIZE = 8;
export const COMPUTE_INTERFACE_ID_MAX = 2;

const MAGIC = new Uint8Array([0x56, 0x43, 0x46, 0x47]); // "VCFG"

export const ACK_STATUS = Object.freeze({
  OK: 0,
  BAD_FRAME: 1,
  UNSUPPORTED_VERSION: 2,
  UNSUPPORTED_MACHINE: 3,
  UNSUPPORTED_PROTOCOL: 4,
  INCOMPATIBLE_SELECTION: 5,
  VCU_NOT_STANDBY: 6,
  FLASH_WRITE_FAILED: 7,
  VERIFY_FAILED: 8,
  UNSUPPORTED_COMPUTE_INTERFACE: 9,
});

export function crc32(bytes) {
  let crc = 0xffffffff;
  for (const byte of bytes) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit += 1) {
      crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
    }
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function writeHeader(view, opcode, payloadLength, sequence) {
  for (let index = 0; index < MAGIC.length; index += 1) {
    view.setUint8(index, MAGIC[index]);
  }
  view.setUint8(4, FRAME_VERSION);
  view.setUint8(5, opcode);
  view.setUint16(6, payloadLength, true);
  view.setUint32(8, sequence >>> 0, true);
}

function finalizeFrame(bytes) {
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const checksum = crc32(bytes.subarray(0, bytes.length - FRAME_CRC_SIZE));
  view.setUint32(bytes.length - FRAME_CRC_SIZE, checksum, true);
  return bytes;
}

export function encodeWriteSelection({
  machineId,
  protocolId,
  computeInterfaceId = 0,
  sequence,
}) {
  if (!Number.isInteger(computeInterfaceId)
      || computeInterfaceId < 0
      || computeInterfaceId > COMPUTE_INTERFACE_ID_MAX) {
    throw new RangeError("INVALID_COMPUTE_INTERFACE_ID");
  }
  const bytes = new Uint8Array(
    FRAME_HEADER_SIZE + SELECTION_PAYLOAD_SIZE + FRAME_CRC_SIZE,
  );
  const view = new DataView(bytes.buffer);
  writeHeader(view, WRITE_SELECTION_OPCODE, SELECTION_PAYLOAD_SIZE, sequence);
  view.setUint16(12, machineId, true);
  view.setUint16(14, protocolId, true);
  view.setUint32(16, computeInterfaceId, true);
  return finalizeFrame(bytes);
}

export function encodeWriteSelectionAck({
  sequence,
  status = ACK_STATUS.OK,
  activeSlot = 0,
  generation = 1,
  machineId,
  protocolId,
}) {
  const bytes = new Uint8Array(
    FRAME_HEADER_SIZE + ACK_PAYLOAD_SIZE + FRAME_CRC_SIZE,
  );
  const view = new DataView(bytes.buffer);
  writeHeader(view, WRITE_SELECTION_ACK_OPCODE, ACK_PAYLOAD_SIZE, sequence);
  view.setUint8(12, status);
  view.setUint8(13, activeSlot);
  view.setUint16(14, generation, true);
  view.setUint16(16, machineId, true);
  view.setUint16(18, protocolId, true);
  return finalizeFrame(bytes);
}

export function expectedFrameLength(bytes) {
  if (bytes.length < FRAME_HEADER_SIZE) return null;
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  return FRAME_HEADER_SIZE + view.getUint16(6, true) + FRAME_CRC_SIZE;
}

export function decodeWriteSelectionAck(bytes, expectedSequence) {
  const expectedLength = FRAME_HEADER_SIZE + ACK_PAYLOAD_SIZE + FRAME_CRC_SIZE;
  if (bytes.length < expectedLength) throw new Error("ACK_FRAME_INCOMPLETE");
  const frame = bytes.subarray(0, expectedLength);
  const view = new DataView(frame.buffer, frame.byteOffset, frame.byteLength);
  for (let index = 0; index < MAGIC.length; index += 1) {
    if (view.getUint8(index) !== MAGIC[index]) throw new Error("ACK_BAD_MAGIC");
  }
  if (view.getUint8(4) !== FRAME_VERSION) throw new Error("ACK_BAD_VERSION");
  if (view.getUint8(5) !== WRITE_SELECTION_ACK_OPCODE) {
    throw new Error("ACK_BAD_OPCODE");
  }
  if (view.getUint16(6, true) !== ACK_PAYLOAD_SIZE) {
    throw new Error("ACK_BAD_LENGTH");
  }
  const sequence = view.getUint32(8, true);
  if (sequence !== (expectedSequence >>> 0)) throw new Error("ACK_BAD_SEQUENCE");
  const receivedCrc = view.getUint32(expectedLength - FRAME_CRC_SIZE, true);
  const calculatedCrc = crc32(frame.subarray(0, expectedLength - FRAME_CRC_SIZE));
  if (receivedCrc !== calculatedCrc) throw new Error("ACK_BAD_CRC");

  return {
    sequence,
    status: view.getUint8(12),
    activeSlot: view.getUint8(13),
    generation: view.getUint16(14, true),
    machineId: view.getUint16(16, true),
    protocolId: view.getUint16(18, true),
  };
}

export function toHex(bytes) {
  return Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0"))
    .join(" ")
    .toUpperCase();
}
