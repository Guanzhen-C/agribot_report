#include "vcu_native_bridge.h"
#include "vcu_runtime.h"
#include "vcu_telemetry.h"
#include "vcfg_protocol.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>
static vcu_bridge_frame_t sent[64];
static unsigned destinations[64], count;
static bool busy;
static bool send_frame(void *context, unsigned destination, const vcu_bridge_frame_t *f) {
    (void)context; if (busy) { return false; }
    sent[count] = *f; destinations[count++] = destination; return true;
}
static void receive(vcu_native_bridge_t *s, unsigned source, uint16_t id, const uint8_t *p, uint32_t at) {
    assert(vcu_bridge_receive(s, source, id, p, 8, false, false, false, at));
}
int main(void) {
    vcu_native_bridge_t s;
    const uint8_t ack[8] = {0xfe,0xd4,0,0,0x01,0x80,0,0}; /* -0.300 m/s, +0.384 rad */
    vcu_bridge_init(&s, 0x12); receive(&s, 1, 0x181, ack, 100);
    vcu_bridge_flush(&s, 101, send_frame, NULL);
    assert(count == 1 && destinations[0] == 0 && sent[0].id == 0x181 && !memcmp(sent[0].data, ack, 8));
    assert(s.targets[0] == -300 && s.targets[1] == 384);
    vcu_bridge_flush(&s, 150, send_frame, NULL); assert(count == 1); /* never replay */
    for (unsigned id = 0x101; id <= 0x103; ++id) { receive(&s, 0, id, ack, 160); }
    vcu_bridge_flush(&s, 161, send_frame, NULL); assert(count == 4 && destinations[3] == 1);
    assert(!vcu_bridge_receive(&s, 0, 0x181, ack, 8, false, false, false, 160));
    assert(!vcu_bridge_receive(&s, 1, 0x181, ack, 8, false, false, true, 160));
    assert(!vcu_bridge_receive(&s, 1, 0x181, ack, 7, false, false, false, 160));
    busy = true; receive(&s, 1, 0x181, ack, 200);
    vcu_bridge_flush(&s, 200, send_frame, NULL); assert(s.queues[0].count == 1);
    busy = false; vcu_bridge_flush(&s, 450, send_frame, NULL); assert(s.dropped == 1 && count == 4);
    for (unsigned i = 0; i < 20; ++i) { receive(&s, 1, 0x181, ack, 500); }
    assert(s.queues[0].count == 16 && s.dropped == 5);
    vcu_bridge_flush(&s, 501, send_frame, NULL); vcu_bridge_flush(&s, 501, send_frame, NULL);
    assert(s.queues[0].count == 0);
    vcu_bridge_init(&s, 1);
    uint8_t diff[8] = {0,0xe5,0x1b,1,0,0,0x0e,0xf1};
    diff[7] = 0; for (unsigned i = 0; i < 7; ++i) { diff[7] ^= diff[i]; }
    receive(&s, 1, 0x514, diff, UINT32_MAX - 10);
    vcu_bridge_flush(&s, 5, send_frame, NULL);
    assert(sent[count-1].id == 0x514 && !memcmp(sent[count-1].data, diff, 8));
    assert(s.targets[0] == -27 && s.targets[1] == 27);
    uint8_t voltage[8] = {3,0,0xee,2,0,0,7,0};
    for (unsigned i = 0; i < 7; ++i) { voltage[7] ^= voltage[i]; }
    receive(&s, 0, 0x532, voltage, 10); assert(s.voltage_mv == 75000 && s.work_mode == 3);
    uint8_t rpm[8] = {0,0x38,0xff,0,0,0,0,0};
    for (unsigned i = 0; i < 7; ++i) { rpm[7] ^= rpm[i]; }
    receive(&s, 0, 0x533, rpm, 10); assert(s.wheel_rpm[0] == -200);
    receive(&s, 0, 0x534, rpm, 10); assert(s.has_wheel[1]);
    assert(!vcu_bridge_receive(&s, 1, 0x181, ack, 8, false, false, false, 10));
    vcu_runtime_t runtime; vcu_runtime_init(&runtime, 1, 8); runtime.bridge = s;
    uint8_t telemetry[80]; vcu_encode_telemetry(&runtime, 0x101, 1, true, true, 20, 42, telemetry);
    assert(telemetry[12] == 2 && telemetry[22] == 13 && telemetry[13] == 15);
    vcu_encode_telemetry(&runtime, 0x101, 1, true, true, 1000, 42, telemetry);
    assert(telemetry[22] == 0 && telemetry[13] == 9);
    uint8_t identity[24]; vcu_encode_identity(1, 0x101, 1, true, 42, identity);
    assert(identity[5] == 0xa1 && identity[12] == 1 && identity[15] == 3);
    puts("native CAN bridge tests passed"); return 0;
}
