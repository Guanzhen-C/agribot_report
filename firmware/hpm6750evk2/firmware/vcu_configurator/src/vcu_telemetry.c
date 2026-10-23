#include "vcu_telemetry.h"
#include "vcfg_protocol.h"
#include <string.h>
static uint32_t get32(const uint8_t *p) { return (uint32_t)p[0] | ((uint32_t)p[1] << 8) | ((uint32_t)p[2] << 16) | ((uint32_t)p[3] << 24); }
static void put16(uint8_t *p, uint16_t v) { p[0] = (uint8_t)v; p[1] = (uint8_t)(v >> 8); }
static void put32(uint8_t *p, uint32_t v) { put16(p, (uint16_t)v); put16(p + 2, (uint16_t)(v >> 16)); }
static bool decode_query(const uint8_t frame[24], uint8_t opcode, uint32_t *sequence)
{
    if (memcmp(frame, "VCFG", 4) != 0 || frame[4] != 1 || frame[5] != opcode
        || frame[6] != 8 || frame[7] != 0 || get32(frame + 20) != vcfg_crc32(frame, 20)) { return false; }
    for (size_t i = 12; i < 20; ++i) { if (frame[i] != 0) { return false; } }
    *sequence = get32(frame + 8); return true;
}
bool vcu_decode_query(const uint8_t frame[24], uint32_t *sequence) {
    return decode_query(frame, VCU_QUERY_OPCODE, sequence);
}
bool vcu_decode_identity_query(const uint8_t frame[24], uint32_t *sequence) {
    return decode_query(frame, VCU_IDENTITY_QUERY_OPCODE, sequence);
}
void vcu_encode_identity(uint16_t machine, uint16_t protocol, uint8_t interface_id,
    bool ready, uint32_t sequence, uint8_t frame[24]) {
    memset(frame, 0, 24); memcpy(frame, "VCFG", 4);
    frame[4] = 1; frame[5] = VCU_IDENTITY_OPCODE; put16(frame + 6, 8); put32(frame + 8, sequence);
    frame[12] = VCU_BRIDGE_VERSION_MAJOR; frame[13] = VCU_BRIDGE_VERSION_MINOR;
    frame[14] = VCU_BRIDGE_VERSION_PATCH; frame[15] = (ready ? 1 : 0) | (vcu_bridge_supported(machine) ? 2 : 0);
    put16(frame + 16, machine); put16(frame + 18, protocol);
    (void)interface_id; /* Native image always uses CAN; interface is in telemetry. */
    put32(frame + 20, vcfg_crc32(frame, 20));
}
void vcu_encode_telemetry(const vcu_runtime_t *s, uint16_t protocol,
    uint8_t compute_interface, bool configured, bool hardware_ready,
    uint32_t now, uint32_t sequence, uint8_t frame[VCU_TELEMETRY_SIZE])
{
    memset(frame, 0, VCU_TELEMETRY_SIZE); memcpy(frame, "VCFG", 4);
    frame[4] = 1; frame[5] = VCU_TELEMETRY_OPCODE; put16(frame + 6, 64); put32(frame + 8, sequence);
    uint8_t *p = frame + 12;
    if (vcu_bridge_supported(s->machine_id)) {
        const vcu_native_bridge_t *b = &s->bridge;
        uint32_t command_ttl = s->machine_id == 0x12 ? 250U : 600U;
        bool input = b->has_command && now - b->command_at < command_ttl;
        bool feedback = b->has_feedback && now - b->feedback_at < 600U;
        uint16_t valid = (b->has_voltage && now - b->voltage_at < 600U ? 1U : 0U)
            | (b->has_motion && now - b->motion_at < 600U ? 2U : 0U)
            | (b->has_wheel[0] && now - b->wheel_at[0] < 600U ? 4U : 0U)
            | (b->has_wheel[1] && now - b->wheel_at[1] < 600U ? 8U : 0U);
        p[0] = 2; /* Native telemetry: angle / signed wheel percentages, not FGCP yaw. */
        p[1] = (configured ? 1 : 0) | (input ? 2 : 0) | (feedback ? 4 : 0) | (hardware_ready ? 8 : 0);
        put16(p + 2, s->fault); put16(p + 4, s->machine_id); put16(p + 6, protocol); p[8] = compute_interface;
        put16(p + 10, valid); put32(p + 12, now);
        put32(p + 16, b->has_command ? now - b->command_at : UINT32_MAX);
        put32(p + 20, b->has_feedback ? now - b->feedback_at : UINT32_MAX);
        if (valid & 1) { put32(p + 24, (uint32_t)b->voltage_mv); }
        if (valid & 2) { put32(p + 28, (uint32_t)b->forward_mm_s); put32(p + 32, (uint32_t)b->yaw_mrad_s); }
        if (input) { put32(p + 36, (uint32_t)(int32_t)b->targets[0]); put32(p + 40, (uint32_t)(int32_t)b->targets[1]); }
        if (valid & 4) { put16(p + 44, (uint16_t)b->wheel_rpm[0]); }
        if (valid & 8) { put16(p + 46, (uint16_t)b->wheel_rpm[1]); }
        p[48] = valid & 1 ? b->work_mode : 0xff; p[49] = valid & 2 ? b->drive_disabled : 0xff;
        put32(p + 52, b->forwarded[0]); put32(p + 56, b->dropped); put32(p + 60, b->forwarded[1]);
        put32(frame + 76, vcfg_crc32(frame, 76)); return;
    }
    bool input_fresh = s->has_command && now - s->command_at < FGCP_VALID_MS;
    bool feedback_fresh = s->has_feedback && now - s->feedback_at <= 300;
    p[0] = 1; /* Telemetry schema, independent of configuration schema. */
    p[1] = (configured ? 1 : 0) | (input_fresh ? 2 : 0) | (feedback_fresh ? 4 : 0)
        | (hardware_ready ? 8 : 0) | (s->enabled && hardware_ready ? 16 : 0);
    put16(p + 2, s->fault); put16(p + 4, s->machine_id); put16(p + 6, protocol); p[8] = compute_interface;
    /* Validity: bit0 voltage, bit1 motion, bit2 SOC, bit3 implement feedback. */
    put16(p + 10, feedback_fresh ? 3 : 0); put32(p + 12, now);
    put32(p + 16, s->has_command ? now - s->command_at : UINT32_MAX);
    put32(p + 20, s->has_feedback ? now - s->feedback_at : UINT32_MAX);
    if (feedback_fresh) {
        put32(p + 24, (uint32_t)(int32_t)s->feedback.voltage_mv);
        put32(p + 28, (uint32_t)(int32_t)s->feedback.velocity.forward_mm_s);
        put32(p + 32, (uint32_t)(int32_t)s->feedback.velocity.yaw_mrad_s);
        p[49] = s->feedback.drive_disabled ? 1 : 0;
    }
    if (input_fresh) {
        put32(p + 36, (uint32_t)(int32_t)s->command.linear_mm_s);
        put32(p + 40, (uint32_t)(int32_t)s->command.angular_mrad_s);
        p[46] = s->command.fan; p[47] = s->command.pump; p[48] = s->command.flags;
        put16(p + 50, s->command.shredder_x100);
    }
    put16(p + 44, UINT16_MAX); /* No reported SOC; never infer a battery percentage. */
    put32(p + 52, s->accepted); put32(p + 56, s->rejected);
    put32(frame + 76, vcfg_crc32(frame, 76));
}
