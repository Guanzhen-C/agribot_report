import assert from "node:assert/strict";
import test from "node:test";
import {
  ACK_STATUS,
  crc32,
  decodeWriteSelectionAck,
  encodeWriteSelection,
  encodeWriteSelectionAck,
  toHex,
} from "../lib/serial-frame.mjs";

test("encodes the fixed 24-byte write-selection frame", () => {
  const frame = encodeWriteSelection({
    machineId: 0x0001,
    protocolId: 0x0101,
    computeInterfaceId: 0x02,
    sequence: 0x12345678,
  });

  assert.equal(frame.length, 24);
  assert.deepEqual(Array.from(frame.subarray(0, 12)), [
    0x56, 0x43, 0x46, 0x47,
    0x01, 0x10, 0x08, 0x00,
    0x78, 0x56, 0x34, 0x12,
  ]);
  assert.deepEqual(Array.from(frame.subarray(12, 20)), [
    0x01, 0x00, 0x01, 0x01, 0x02, 0x00, 0x00, 0x00,
  ]);
  const view = new DataView(frame.buffer);
  assert.equal(view.getUint32(20, true), crc32(frame.subarray(0, 20)));
  assert.match(toHex(frame), /^56 43 46 47 01 10 08 00/);
});

test("rejects an unknown compute interface id", () => {
  assert.throws(
    () => encodeWriteSelection({
      machineId: 0x0001,
      protocolId: 0x0101,
      computeInterfaceId: 3,
      sequence: 1,
    }),
    /INVALID_COMPUTE_INTERFACE_ID/,
  );
});

test("validates sequence, CRC and selected IDs in an ACK", () => {
  const ackFrame = encodeWriteSelectionAck({
    sequence: 7,
    status: ACK_STATUS.OK,
    activeSlot: 1,
    generation: 23,
    machineId: 0x0001,
    protocolId: 0x0101,
  });
  const ack = decodeWriteSelectionAck(ackFrame, 7);
  assert.deepEqual(ack, {
    sequence: 7,
    status: 0,
    activeSlot: 1,
    generation: 23,
    machineId: 0x0001,
    protocolId: 0x0101,
  });

  assert.throws(() => decodeWriteSelectionAck(ackFrame, 8), /ACK_BAD_SEQUENCE/);
  const damaged = new Uint8Array(ackFrame);
  damaged[16] ^= 0x01;
  assert.throws(() => decodeWriteSelectionAck(damaged, 7), /ACK_BAD_CRC/);
});
