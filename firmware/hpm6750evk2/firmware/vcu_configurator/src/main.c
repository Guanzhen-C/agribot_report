#include <stdbool.h>
#include <stdint.h>
#include <string.h>

#include "board.h"
#include "hpm_uart_drv.h"
#include "vcfg_protocol.h"
#include "vcu_board_io.h"
#include "vcu_flash_backend.h"
#include "vcu_selection_store.h"
#include "vcu_machine_profile.h"
#include "vcu_runtime_io.h"
#include "vcu_telemetry.h"

#define SUPPORTED_MACHINE_ID VCU_MACHINE_TRACKED
#define SUPPORTED_PROTOCOL_ID VCU_PROTOCOL_IMPLEMENT
#ifndef VCU_BRIDGE_MACHINE_ID
#define VCU_BRIDGE_MACHINE_ID 0
#endif

#ifndef VCU_CONFIG_SELF_TEST
#define VCU_CONFIG_SELF_TEST 0
#endif

#if VCU_CONFIG_SELF_TEST
volatile uint32_t vcu_self_test_status;
#endif

static uint8_t reply_bytes[VCU_TELEMETRY_SIZE];
static size_t reply_length, reply_position;
static void send_frame(const uint8_t *frame, size_t length)
{
    memcpy(reply_bytes, frame, length); reply_length = length; reply_position = 0;
}
static void poll_reply(void)
{
    /* UART diagnostics must not block CAN forwarding for a whole 80-byte reply. */
    unsigned budget = 8;
    while (reply_position < reply_length && budget--
        && (uart_get_status(BOARD_CONSOLE_UART_BASE) & uart_stat_tx_slot_avail)) {
        uart_write_byte(BOARD_CONSOLE_UART_BASE, reply_bytes[reply_position++]);
    }
}

int main(void)
{
    board_init();

    vcu_store_backend_t store_backend;
    bool flash_ready = vcu_flash_backend_init(&store_backend);
    vcu_selection_t active_selection = {0};
    bool has_active_selection = flash_ready
        && vcu_store_load(&store_backend, &active_selection);
    if (has_active_selection) {
        vcfg_write_request_t stored = {
            .machine_id = active_selection.machine_id,
            .protocol_id = active_selection.protocol_id,
            .compute_interface_id = active_selection.compute_interface_id,
        };
        has_active_selection = vcu_validate_selection(&stored) == VCFG_STATUS_OK;
    }
    if (has_active_selection) {
        vcu_board_route_compute_interface(
            active_selection.compute_interface_id);
    }
#if VCU_BRIDGE_MACHINE_ID != 0
    /* A profile-specific image is authoritative, never an old flash selection.
     * The stored A/B records are retained for the maintenance image. */
    active_selection.machine_id = VCU_BRIDGE_MACHINE_ID;
    active_selection.protocol_id = VCU_BRIDGE_MACHINE_ID == VCU_MACHINE_STEERING
        ? VCU_PROTOCOL_STEERING : VCU_PROTOCOL_IMPLEMENT;
    active_selection.compute_interface_id = VCFG_COMPUTE_INTERFACE_CAN;
    has_active_selection = true;
#endif

#if VCU_CONFIG_SELF_TEST
    if (flash_ready) {
        vcu_store_status_t self_test_status = vcu_store_save(
            &store_backend, SUPPORTED_MACHINE_ID, SUPPORTED_PROTOCOL_ID,
            VCFG_COMPUTE_INTERFACE_SERIAL,
            &active_selection);
        has_active_selection = self_test_status == VCU_STORE_OK
            && vcu_store_load(&store_backend, &active_selection);
        vcu_self_test_status = has_active_selection
            ? VCFG_STATUS_OK : VCFG_STATUS_VERIFY_FAILED;
    } else {
        vcu_self_test_status = VCFG_STATUS_FLASH_WRITE_FAILED;
    }
#endif

    vcfg_parser_t parser;
    vcfg_parser_init(&parser);
    vcu_runtime_t runtime;
    uint8_t runtime_interface = has_active_selection ? active_selection.compute_interface_id : 0;
    uint16_t runtime_protocol = has_active_selection ? active_selection.protocol_id : 0;
    vcu_runtime_init(&runtime, has_active_selection ? active_selection.machine_id : 0,
        runtime_interface == VCFG_COMPUTE_INTERFACE_SERIAL ? 16 : 8);
    bool runtime_configured = has_active_selection;
    bool hardware_ready = has_active_selection && vcu_runtime_io_init(runtime.machine_id, runtime_interface);

    while (true) {
        uint32_t now = vcu_runtime_now();
        vcu_runtime_io_poll(&runtime, runtime_interface, now);
        poll_reply();
        if (reply_position < reply_length) { continue; }
        uint8_t byte;
        if (uart_try_receive_byte(BOARD_CONSOLE_UART_BASE, &byte)
            != status_success) {
            continue;
        }
        uint8_t request_frame[VCFG_FRAME_SIZE];
        if (!vcfg_parser_feed(&parser, byte, request_frame)) {
            continue;
        }

        uint32_t query_sequence;
        if (vcu_decode_identity_query(request_frame, &query_sequence)) {
            uint8_t identity[VCFG_FRAME_SIZE];
            vcu_encode_identity(runtime.machine_id, runtime_protocol, runtime_interface,
                hardware_ready, query_sequence, identity);
            send_frame(identity, sizeof(identity));
            continue;
        }
        if (vcu_decode_query(request_frame, &query_sequence)) {
            uint8_t telemetry[VCU_TELEMETRY_SIZE];
            vcu_encode_telemetry(&runtime, runtime_protocol, runtime_interface,
                runtime_configured, hardware_ready, now, query_sequence, telemetry);
            send_frame(telemetry, sizeof(telemetry));
            continue;
        }

        vcfg_write_request_t request = {0};
        vcfg_status_t status = vcfg_decode_write_request(request_frame,
                                                         &request);
        if (status == VCFG_STATUS_OK) {
            status = vcu_validate_selection(&request);
        }
        if (status == VCFG_STATUS_OK && (hardware_ready || VCU_BRIDGE_MACHINE_ID != 0)) {
            /* Flash erase must never suspend a running safety scheduler. Configure
             * with the maintenance image, then boot the validated runtime image. */
            status = VCFG_STATUS_VCU_NOT_STANDBY;
        }
        if (status == VCFG_STATUS_OK && !flash_ready) {
            status = VCFG_STATUS_FLASH_WRITE_FAILED;
        }
        if (status == VCFG_STATUS_OK) {
            vcu_store_status_t store_status = vcu_store_save(
                &store_backend, request.machine_id, request.protocol_id,
                request.compute_interface_id,
                &active_selection);
            if (store_status == VCU_STORE_WRITE_FAILED) {
                status = VCFG_STATUS_FLASH_WRITE_FAILED;
            } else if (store_status == VCU_STORE_VERIFY_FAILED) {
                status = VCFG_STATUS_VERIFY_FAILED;
            } else {
                has_active_selection = true;
            }
        }

        vcfg_write_ack_t ack = {
            .sequence = request.sequence,
            .status = status,
            .active_slot = has_active_selection
                ? active_selection.active_slot : 0U,
            .generation = has_active_selection
                ? (uint16_t)active_selection.generation : 0U,
            .machine_id = has_active_selection
                ? active_selection.machine_id : request.machine_id,
            .protocol_id = has_active_selection
                ? active_selection.protocol_id : request.protocol_id,
        };
        uint8_t ack_frame[VCFG_FRAME_SIZE];
        vcfg_encode_write_ack(&ack, ack_frame);
        send_frame(ack_frame, sizeof(ack_frame));
    }
}
