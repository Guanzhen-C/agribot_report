#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd -- "$(dirname -- "$0")" && pwd)"
HARDWARE="${1:?usage: run_ros_protocol_tests.sh /path/to/agribot_hardware_bringup}"
TEMP="$(mktemp -d)"
trap 'rm -rf "$TEMP"' EXIT
cc -std=c11 -Wall -Wextra -Werror -I"$HERE/../src" -c "$HERE/../src/vcu_native_bridge.c" -o "$TEMP/bridge.o"
c++ -std=c++17 -Wall -Wextra -Werror \
  -I"$HERE/../src" -I"$HARDWARE/can/include" -I"$HARDWARE/ackermann/include" -I"$HARDWARE/differential/include" \
  "$HERE/test_ros_native_bridge.cpp" "$TEMP/bridge.o" \
  "$HARDWARE/ackermann/src/ackermann_can_protocol.cpp" \
  "$HARDWARE/differential/src/differential_can_protocol.cpp" \
  "$HARDWARE/can/src/chassis_can_common.cpp" -o "$TEMP/ros_bridge_test"
"$TEMP/ros_bridge_test"
