#include <cassert>
#include <cstring>
#include <iostream>
#include "agribot_hardware_bringup/ackermann_can_protocol.hpp"
#include "agribot_hardware_bringup/differential_can_protocol.hpp"
extern "C" {
#include "vcu_native_bridge.h"
}
static vcu_bridge_frame_t forwarded;
static unsigned forwarded_destination;
static bool send_frame(void *, unsigned destination, const vcu_bridge_frame_t *frame) {
    forwarded = *frame; forwarded_destination = destination; return true;
}
static void through(vcu_native_bridge_t &bridge, unsigned source,
    const agribot_hardware_bringup::chassis_can::Frame &frame) {
    assert(vcu_bridge_receive(&bridge, source, frame.id, frame.data.data(), 8, false, false, false, 100));
    vcu_bridge_flush(&bridge, 101, send_frame, nullptr);
    assert(forwarded_destination == 1 - source && forwarded.id == frame.id);
    assert(!std::memcmp(forwarded.data, frame.data.data(), 8));
}
int main() {
    namespace ack = agribot_hardware_bringup::ackermann_can;
    namespace diff = agribot_hardware_bringup::differential_can;
    namespace can = agribot_hardware_bringup::chassis_can;
    vcu_native_bridge_t bridge; vcu_bridge_init(&bridge, 18);
    for (double speed : {-0.3, 0.0, 0.3}) {
        for (double steering : {-0.384, 0.0, 0.384}) {
            through(bridge, 1, ack::encodeCommand({speed, steering}));
            assert(bridge.targets[0] == int(speed * 1000) && bridge.targets[1] == int(steering * 1000));
        }
    }
    vcu_bridge_init(&bridge, 1);
    for (unsigned counter = 0; counter < 16; ++counter) {
        through(bridge, 1, diff::encodeCommand({-27, 27, true}, counter));
        assert(bridge.targets[0] == -27 && bridge.targets[1] == 27);
    }
    can::Frame voltage{0x532, {3, 0, 0xee, 2, 0, 0, 7, 0}};
    voltage.data[7] = can::xorChecksum(voltage.data);
    through(bridge, 0, voltage);
    assert(diff::decodeChassisState(voltage)->battery_voltage == bridge.voltage_mv / 1000.0);
    can::Frame rpm{0x533, {0, 0x38, 0xff, 0, 0, 0, 0, 0}};
    rpm.data[7] = can::xorChecksum(rpm.data);
    through(bridge, 0, rpm);
    assert(diff::decodeMotorState(rpm)->speed == bridge.wheel_rpm[0]);
    std::cout << "actual ROS CAN encoders and bridge are byte-identical\n";
}
