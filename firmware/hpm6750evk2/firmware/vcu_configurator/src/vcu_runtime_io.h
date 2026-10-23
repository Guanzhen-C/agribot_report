#ifndef VCU_RUNTIME_IO_H
#define VCU_RUNTIME_IO_H
#include "vcu_runtime.h"
uint32_t vcu_runtime_now(void);
bool vcu_runtime_io_init(uint16_t machine_id, uint8_t compute_interface);
void vcu_runtime_io_poll(vcu_runtime_t *state, uint8_t compute_interface, uint32_t now);
#endif
