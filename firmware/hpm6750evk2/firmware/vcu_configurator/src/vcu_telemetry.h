#ifndef VCU_TELEMETRY_H
#define VCU_TELEMETRY_H
#include "vcu_runtime.h"
#define VCU_TELEMETRY_SIZE 80U
#define VCU_QUERY_OPCODE 0x20U
#define VCU_TELEMETRY_OPCODE 0xa0U
#define VCU_IDENTITY_QUERY_OPCODE 0x21U
#define VCU_IDENTITY_OPCODE 0xa1U
bool vcu_decode_query(const uint8_t frame[24], uint32_t *sequence);
bool vcu_decode_identity_query(const uint8_t frame[24], uint32_t *sequence);
void vcu_encode_identity(uint16_t machine, uint16_t protocol, uint8_t interface_id,
    bool ready, uint32_t sequence, uint8_t frame[24]);
void vcu_encode_telemetry(const vcu_runtime_t *runtime, uint16_t protocol,
    uint8_t compute_interface, bool configured, bool hardware_ready,
    uint32_t now, uint32_t sequence, uint8_t frame[VCU_TELEMETRY_SIZE]);
#endif
