import test from "node:test";
import assert from "node:assert/strict";
import { crc32 } from "../lib/serial-frame.mjs";
import { decodeTelemetry } from "../lib/runtime-frame.mjs";

function frame(machine) {
  const bytes = new Uint8Array(80), v = new DataView(bytes.buffer);
  bytes.set([0x56,0x43,0x46,0x47,1,0xa0,64,0]); v.setUint32(8, 42, true);
  bytes[12] = 2; bytes[13] = 15; bytes[20] = 1;
  v.setUint16(16, machine, true); v.setUint16(22, 13, true);
  v.setInt32(36, 75000, true); v.setInt32(48, -27, true); v.setInt32(52, 384, true);
  v.setInt16(56, -200, true); v.setInt16(58, 200, true); bytes[60] = bytes[61] = 255;
  v.setUint32(76, crc32(bytes.subarray(0,76)), true); return bytes;
}
test("native angle and wheel targets keep distinct meanings", () => {
  const ack = decodeTelemetry(frame(18), 42);
  assert.equal(ack.targetSteeringAngle, .384); assert.equal(ack.targetYawRate, null);
  assert.equal(ack.outputEnabled, null); assert.equal(ack.batteryPercent, null);
  const diff = decodeTelemetry(frame(1), 42);
  assert.equal(diff.targetSpeed, null); assert.deepEqual(diff.targetWheelPercent, [-27,384]);
  assert.equal(diff.voltage, 75); assert.equal(diff.leftRpm, -200);
  assert.equal(diff.workMode, null);
});
test("native telemetry rejects corruption and wrong query", () => {
  const bytes = frame(18); bytes[40] ^= 1;
  assert.throws(() => decodeTelemetry(bytes, 42), /CRC/);
  assert.throws(() => decodeTelemetry(frame(18), 41), /SEQUENCE/);
});
