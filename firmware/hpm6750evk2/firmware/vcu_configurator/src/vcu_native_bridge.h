#ifndef VCU_NATIVE_BRIDGE_H
#define VCU_NATIVE_BRIDGE_H
#include <stdbool.h>
#include <stdint.h>
#define VCU_BRIDGE_QUEUE_SIZE 16U
#define VCU_BRIDGE_VERSION_MAJOR 1U
#define VCU_BRIDGE_VERSION_MINOR 0U
#define VCU_BRIDGE_VERSION_PATCH 0U
typedef struct { uint16_t id; uint8_t data[8]; uint32_t at; } vcu_bridge_frame_t;
typedef struct {
    vcu_bridge_frame_t frames[VCU_BRIDGE_QUEUE_SIZE];
    uint8_t head, count;
} vcu_bridge_queue_t;
typedef struct {
    uint16_t machine_id;
    vcu_bridge_queue_t queues[2]; /* destination: 0 actuator, 1 compute */
    uint32_t received[2], forwarded[2], dropped, blocked, tx_busy;
    uint32_t command_at, feedback_at, voltage_at, motion_at, wheel_at[2];
    bool has_command, has_feedback, has_voltage, has_motion, has_wheel[2];
    int32_t voltage_mv, forward_mm_s, yaw_mrad_s;
    int16_t targets[2], wheel_rpm[2];
    uint8_t work_mode, drive_disabled;
    uint8_t feedback_bytes[24], feedback_part;
    uint32_t feedback_started;
} vcu_native_bridge_t;
typedef bool (*vcu_bridge_send_t)(void *context, unsigned destination,
    const vcu_bridge_frame_t *frame);
bool vcu_bridge_supported(uint16_t machine_id);
void vcu_bridge_init(vcu_native_bridge_t *state, uint16_t machine_id);
bool vcu_bridge_receive(vcu_native_bridge_t *state, unsigned source,
    uint16_t id, const uint8_t *data, unsigned dlc, bool extended,
    bool remote, bool fd, uint32_t now);
void vcu_bridge_flush(vcu_native_bridge_t *state, uint32_t now,
    vcu_bridge_send_t send, void *context);
#endif
