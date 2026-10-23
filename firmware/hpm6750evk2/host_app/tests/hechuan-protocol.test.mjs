import assert from "node:assert/strict";
import test from "node:test";
import {
  HECHUAN_CAN_BITRATE, HECHUAN_SERIAL_BAUD_RATE,
  encodeSerialMotion, decodeSerialMotion, encodeVelocityCanMotion,
  encodeSteeringCanMotion, decodeMotionFeedback, xorChecksum,
  MotionFeedbackStream, MotionCanFeedback,
} from "../lib/hechuan-protocol.mjs";

const velocity = { forward: 1, lateral: -0.5, yawRate: 0.25 };
const feedback = Uint8Array.from([
  0x7b, 1, 3, 0xe8, 0xfe, 0x0c, 0, 0xfa,
  0, 1, 0xff, 0xfe, 0x40, 0, 0, 3, 0xff, 0xfc, 0, 5, 0x5d, 0xc0, 0, 0x7d,
]);
feedback[22] = xorChecksum(feedback.subarray(0, 22));

test("motion wire format uses signed big-endian thousandths", () => {
  assert.equal(HECHUAN_CAN_BITRATE, 1000000);
  assert.equal(HECHUAN_SERIAL_BAUD_RATE, 115200);
  const frame = encodeSerialMotion(velocity);
  assert.deepEqual([...frame.slice(0, 9)], [0x7b, 0, 0, 3, 0xe8, 0xfe, 0x0c, 0, 0xfa]);
  assert.equal(frame[9], 0x98);
  assert.equal(frame[10], 0x7d);
  assert.deepEqual(decodeSerialMotion(frame), velocity);
  const can = encodeVelocityCanMotion(velocity);
  assert.equal(can.id, 0x181);
  assert.equal(can.extended, false);
  assert.deepEqual([...can.data], [3, 0xe8, 0xfe, 0x0c, 0, 0xfa, 0, 0]);
  assert.deepEqual([...encodeSteeringCanMotion({ forward: 1, steeringAngle: -0.25 }).data],
    [3, 0xe8, 0, 0, 0xff, 0x06, 0, 0]);
});

test("rejects invalid values, checksum, framing and non-motion modes", () => {
  for (const forward of [NaN, Infinity, -Infinity, 32.768, -32.769]) {
    assert.throws(() => encodeSerialMotion({ forward }), RangeError);
  }
  for (const forward of [32.767, -32.768]) {
    assert.equal(decodeSerialMotion(encodeSerialMotion({ forward })).forward, forward);
  }
  const bad = encodeSerialMotion(velocity);
  bad[9] ^= 1;
  assert.throws(() => decodeSerialMotion(bad), /BAD_FRAME/);
  const mode = encodeSerialMotion(); mode[1] = 0xff;
  mode[9] = xorChecksum(mode.subarray(0, 9));
  assert.throws(() => decodeSerialMotion(mode), /UNSUPPORTED_MODE/);
  assert.throws(() => decodeSerialMotion(bad.subarray(1)), /BAD_FRAME/);
});

test("feedback keeps raw IMU values and drive-disable distinct from emergency stop", () => {
  assert.deepEqual(decodeMotionFeedback(feedback), {
    driveDisabled: true, ...velocity, accelerationRaw: [1, -2, 16384],
    angularVelocityRaw: [3, -4, 5], voltage: 24,
  });
  const corrupt = feedback.slice(); corrupt[8] ^= 1;
  assert.throws(() => decodeMotionFeedback(corrupt), /BAD_FRAME/);
  corrupt.set(feedback); corrupt[1] = 2; corrupt[22] = xorChecksum(corrupt.subarray(0, 22));
  assert.throws(() => decodeMotionFeedback(corrupt), /BAD_STATE/);
});

test("serial feedback recovers from noise, corruption and every split boundary", () => {
  for (let split = 1; split < feedback.length; split++) {
    const stream = new MotionFeedbackStream();
    assert.equal(stream.feed(Uint8Array.from([0xfa, 0, 0xfc])).length, 0);
    assert.equal(stream.feed(feedback.slice(0, split)).length, 0);
    assert.equal(stream.feed(feedback.slice(split)).length, 1);
    assert.equal(stream.buffer.length, 0);
  }
  const corrupt = feedback.slice(); corrupt[22] ^= 1;
  const stream = new MotionFeedbackStream();
  assert.equal(stream.feed(Uint8Array.from([...corrupt, ...feedback, ...feedback])).length, 2);
});

test("CAN feedback requires ordered standard data frames within 100 ms", () => {
  const part = (index, extra = {}) => ({ id: 0x101 + index, data: feedback.slice(index * 8, index * 8 + 8), ...extra });
  const receiver = new MotionCanFeedback();
  assert.equal(receiver.feed(part(1), 0), null);
  assert.equal(receiver.feed(part(0), 1), null);
  assert.equal(receiver.feed(part(1), 2), null);
  assert.equal(receiver.feed(part(2), 3).voltage, 24);
  let time = 4;
  for (const extra of [{ extended: true }, { remote: true }, { data: new Uint8Array(7) }]) {
    receiver.feed(part(0), time++); receiver.feed(part(1, extra), time++);
    assert.equal(receiver.feed(part(2), time++), null);
  }
  receiver.feed(part(0), 20); receiver.feed(part(1), 21);
  assert.equal(receiver.feed(part(2), 121), null);
  receiver.feed(part(0), 200);
  assert.equal(receiver.feed(part(2), 201), null);
  assert.equal(receiver.feed(part(1), 202), null);
});
