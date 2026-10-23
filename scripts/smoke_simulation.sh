#!/usr/bin/env bash
# A bounded delivery smoke test, not a route-accuracy acceptance test.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/scripts/env.sh"
export ROS_DOMAIN_ID=93 ROS_LOCALHOST_ONLY=1
export RMW_IMPLEMENTATION=rmw_cyclonedds_cpp
export CYCLONEDDS_URI="file://$ROOT/ros2_ws/src/agribot_vehicle_description/config/cyclonedds_simulation.xml"
mkdir -p "$ROOT/.validation"
ros2 launch agribot_vehicle_description configured_ackermann_sim.launch.py \
  vehicle_id:=ackermann_current scenario:=orchard rviz:=false gui:=false \
  run_waypoints:=false > "$ROOT/.validation/simulation.log" 2>&1 &
pid=$!
cleanup() {
  kill -INT "$pid" 2>/dev/null || true
  for _ in {1..20}; do
    kill -0 "$pid" 2>/dev/null || break
    sleep 1
  done
  if kill -0 "$pid" 2>/dev/null; then
    kill -TERM "$pid" 2>/dev/null || true
  fi
  wait "$pid" 2>/dev/null || true
}
trap cleanup EXIT
sleep "${AGRIBOT_SMOKE_STARTUP_SECONDS:-45}"
kill -0 "$pid"
ros2 topic list --no-daemon > "$ROOT/.validation/simulation_topics.txt"
timeout 20s ros2 topic echo /clock --once --no-daemon > "$ROOT/.validation/clock.txt"
echo 'Simulation started and published /clock. Inspect the log and topic list for sensor/localization status.'
