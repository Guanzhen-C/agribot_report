import assert from 'node:assert/strict';
import test from 'node:test';
import { decodeThreeInOneFrame } from '../lib/three-in-one-protocol.mjs';

function frame(raw) {
  const bytes = new Uint8Array(8); bytes.set(raw);
  bytes[7] = bytes.subarray(0, 7).reduce((a, b) => a ^ b, 0);
  return bytes;
}

test('three-in-one golden battery frame separates battery SOC from remote SOC', () => {
  const result = decodeThreeInOneFrame(0x650, new Uint8Array([8, 2, 248, 0, 12, 3, 2, 255]));
  assert.equal(result.kind, 'battery');
  assert.equal(result.counter, 2);
  assert.equal(result.values.voltage, 52);
  assert.equal(result.values.current, 24.8);
  assert.equal(result.values.soc, 78);
  const remote = decodeThreeInOneFrame(0x501, frame([0, 0, 0, 0, 62, 4, 15]));
  assert.equal(remote.values.soc, 62);
  assert.equal(remote.counter, 15);
  assert.equal(remote.values.version, 4);
});

test('signed current and RPM, temperature offset, bit-aligned states', () => {
  const drive = decodeThreeInOneFrame(0x533, frame([0, 0x18, 0xfc, 52, 0xf8, 30, 3])).values;
  assert.deepEqual(drive, { faults: 0, rpm: -1000, voltage: 52, current: -8, temperature: -10 });
  assert.equal(decodeThreeInOneFrame(0x534, frame([0, 0, 0, 0, 0, 40])).values.temperature, 0);
  assert.equal(decodeThreeInOneFrame(0x650, frame([8, 2, 0x85, 0xff, 0xe8, 3])).values.current, -12.3);
  const pump = decodeThreeInOneFrame(0x5b3, frame([0x53, 0xdc, 5, 52, 3])).values;
  assert.equal(pump.gear, 3); assert.equal(pump.reverse, 1); assert.equal(pump.liquidLevel, 1); assert.equal(pump.rpm, 1500);
  const vehicle = decodeThreeInOneFrame(0x532, frame([13, 4, 8, 2, 10])).values;
  assert.deepEqual(vehicle, { mode: 1, emergency: 1, moving: 1, light: 1, voltage: 52, communicationFaults: 10 });
});

test('invalid or reserved feedback is never accepted as a valid dashboard value', () => {
  assert.throws(() => decodeThreeInOneFrame(0x650, new Uint8Array(7)), /CAN_LENGTH/);
  assert.throws(() => decodeThreeInOneFrame(0x650, Array(8).fill(0)), /CAN_LENGTH/);
  for (const id of [-1, 0x800, 1.5]) assert.throws(() => decodeThreeInOneFrame(id, frame([])), /CAN_ID/);
  assert.throws(() => decodeThreeInOneFrame(0x514, frame([])), /CAN_UNSUPPORTED/);
  const corrupt = frame([8, 2]); corrupt[0] ^= 1;
  assert.throws(() => decodeThreeInOneFrame(0x650, corrupt), /CAN_CHECKSUM/);
  assert.equal(decodeThreeInOneFrame(0x650, frame([0, 0, 0, 0, 0xe8, 3])).values.soc, 100);
  assert.equal(decodeThreeInOneFrame(0x650, frame([0, 0, 0, 0, 0xe9, 3])).values.soc, null);
  assert.equal(decodeThreeInOneFrame(0x501, frame([0, 0, 0, 0, 101])).values.soc, null);
  assert.equal(decodeThreeInOneFrame(0x5b2, frame([15])).values.gear, null);
  assert.equal(decodeThreeInOneFrame(0x532, frame([3])).values.mode, null);
});
