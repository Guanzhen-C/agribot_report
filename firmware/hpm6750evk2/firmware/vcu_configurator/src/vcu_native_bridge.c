#include "vcu_native_bridge.h"
#include "vcu_machine_profile.h"
#include <string.h>
static int16_t be16(const uint8_t *p) { return (int16_t)((uint16_t)p[0] << 8 | p[1]); }
static int16_t le16(const uint8_t *p) { return (int16_t)((uint16_t)p[1] << 8 | p[0]); }
static bool xor_ok(const uint8_t *p, unsigned n) {
    uint8_t checksum = 0;
    for (unsigned i = 0; i < n; ++i) { checksum ^= p[i]; }
    return checksum == p[n];
}
bool vcu_bridge_supported(uint16_t id) {
    return id == VCU_MACHINE_STEERING || id == VCU_MACHINE_TRACKED;
}
void vcu_bridge_init(vcu_native_bridge_t *s, uint16_t id) {
    memset(s, 0, sizeof(*s)); s->machine_id = id;
    s->work_mode = s->drive_disabled = 0xff;
}
static void observe(vcu_native_bridge_t *s, unsigned source, uint16_t id,
    const uint8_t *data, uint32_t now) {
    if (source == 1) {
        s->has_command = true; s->command_at = now;
        if (s->machine_id == VCU_MACHINE_STEERING) {
            s->targets[0] = be16(data); s->targets[1] = be16(data + 4);
        } else if (xor_ok(data, 7)) {
            s->targets[0] = (int8_t)data[1]; s->targets[1] = (int8_t)data[2];
        }
        return;
    }
    s->has_feedback = true; s->feedback_at = now;
    if (s->machine_id == VCU_MACHINE_TRACKED) {
        if (!xor_ok(data, 7)) { return; }
        if (id == 0x532) {
            s->voltage_mv = (uint16_t)le16(data + 2) * 100;
            s->voltage_at = now; s->has_voltage = true;
            s->work_mode = data[0] & 3U;
        } else {
            unsigned wheel = id == 0x533 ? 0 : 1;
            s->wheel_rpm[wheel] = le16(data + 1);
            s->wheel_at[wheel] = now; s->has_wheel[wheel] = true;
        }
        return;
    }
    if (id == 0x101) { s->feedback_part = 0; s->feedback_started = now; }
    if (id != 0x101U + s->feedback_part || now - s->feedback_started > 100U) {
        s->feedback_part = 0; return;
    }
    memcpy(s->feedback_bytes + 8U * s->feedback_part, data, 8);
    if (++s->feedback_part != 3) { return; }
    s->feedback_part = 0;
    const uint8_t *p = s->feedback_bytes;
    if (p[0] != 0x7b || p[23] != 0x7d || p[1] > 1 || !xor_ok(p, 22)) { return; }
    s->voltage_mv = (uint16_t)be16(p + 20);
    s->forward_mm_s = be16(p + 2); s->yaw_mrad_s = be16(p + 6);
    s->drive_disabled = p[1]; s->voltage_at = s->motion_at = now;
    s->has_voltage = s->has_motion = true;
}
bool vcu_bridge_receive(vcu_native_bridge_t *s, unsigned source, uint16_t id,
    const uint8_t *data, unsigned dlc, bool extended, bool remote, bool fd, uint32_t now) {
    bool allowed = source <= 1 && vcu_bridge_supported(s->machine_id);
    if (allowed && s->machine_id == VCU_MACHINE_STEERING) {
        allowed = source == 1 ? id == 0x181 : id >= 0x101 && id <= 0x103;
    } else if (allowed) {
        allowed = source == 1 ? id == 0x514 : id >= 0x532 && id <= 0x534;
    }
    if (!allowed || !data || dlc != 8 || extended || remote || fd) { ++s->blocked; return false; }
    ++s->received[source]; observe(s, source, id, data, now);
    vcu_bridge_queue_t *q = &s->queues[1U - source];
    if (q->count == VCU_BRIDGE_QUEUE_SIZE) {
        q->head = (q->head + 1U) % VCU_BRIDGE_QUEUE_SIZE; --q->count; ++s->dropped;
    }
    vcu_bridge_frame_t *frame = &q->frames[(q->head + q->count) % VCU_BRIDGE_QUEUE_SIZE];
    frame->id = id; frame->at = now; memcpy(frame->data, data, 8); ++q->count;
    return true;
}
void vcu_bridge_flush(vcu_native_bridge_t *s, uint32_t now,
    vcu_bridge_send_t send, void *context) {
    for (unsigned destination = 0; destination < 2; ++destination) {
        vcu_bridge_queue_t *q = &s->queues[destination];
        unsigned budget = 8;
        uint32_t ttl = destination == 0 && s->machine_id == VCU_MACHINE_STEERING ? 250U : 600U;
        while (q->count && budget--) {
            vcu_bridge_frame_t *frame = &q->frames[q->head];
            if (now - frame->at >= ttl) { ++s->dropped; }
            else if (!send(context, destination, frame)) { ++s->tx_busy; break; }
            else { ++s->forwarded[destination]; }
            q->head = (q->head + 1U) % VCU_BRIDGE_QUEUE_SIZE; --q->count;
        }
    }
}
