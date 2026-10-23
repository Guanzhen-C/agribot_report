export const FRAME_VERSION: number;
export const WRITE_SELECTION_OPCODE: number;
export const WRITE_SELECTION_ACK_OPCODE: number;
export const FRAME_HEADER_SIZE: number;
export const FRAME_CRC_SIZE: number;
export const SELECTION_PAYLOAD_SIZE: number;
export const ACK_PAYLOAD_SIZE: number;
export const ACK_STATUS: Readonly<Record<string, number>>;

export function crc32(bytes: Uint8Array): number;
export function encodeWriteSelection(input: {
  machineId: number;
  protocolId: number;
  sequence: number;
  flags?: number;
}): Uint8Array;
export function encodeWriteSelectionAck(input: {
  sequence: number;
  status?: number;
  activeSlot?: number;
  generation?: number;
  machineId: number;
  protocolId: number;
}): Uint8Array;
export function expectedFrameLength(bytes: Uint8Array): number | null;
export function decodeWriteSelectionAck(
  bytes: Uint8Array,
  expectedSequence: number,
): {
  sequence: number;
  status: number;
  activeSlot: number;
  generation: number;
  machineId: number;
  protocolId: number;
};
export function toHex(bytes: Uint8Array): string;
