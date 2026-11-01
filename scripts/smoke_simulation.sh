#!/usr/bin/env bash
# A bounded delivery smoke test, not a route-accuracy acceptance test.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/scripts/env.sh"
export ROS_DOMAIN_ID=93 ROS_LOCALHOST_ONLY=1
export RMW_IMPLEMENTATION=rmw_cyclonedds_cpp
export CYCLONEDDS_URI="file://$ROOT/ros2_ws/src/agribot_vehicle_description/config/cyclonedds_simulation.xml"
mkdir -p "$ROOT/.validation"
setsid ros2 launch agribot_vehicle_description configured_ackermann_sim.launch.py \
  vehicle_id:=ackermann_current scenario:=orchard rviz:=false gui:=false \
  run_waypoints:=false > "$ROOT/.validation/simulation.log" 2>&1 &
pid=$!
session_pids() {
  ps -eo pid=,sid=,stat= | awk -v target="$pid" '$2 == target && $3 !~ /^Z/ { print $1 }'
}
signal_session() {
  local -a pids=()
  mapfile -t pids < <(session_pids)
  ((${#pids[@]} == 0)) || kill -s "$1" -- "${pids[@]}" 2>/dev/null || true
}
wait_session() {
  local attempt
  for ((attempt=0; attempt<$1; attempt++)); do
    [[ -n "$(session_pids)" ]] || return 0
    sleep 0.2
  done
  [[ -z "$(session_pids)" ]]
}
cleanup() {
  local result=$?
  trap - EXIT INT TERM
  signal_session INT
  if ! wait_session 50; then
    signal_session TERM
    if ! wait_session 25; then
      signal_session KILL
      if ! wait_session 10; then
        echo "Failed to stop simulation session $pid." >&2
        result=1
      fi
    fi
  fi
  wait "$pid" 2>/dev/null || true
  exit "$result"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
for _ in {1..50}; do
  [[ -z "$(session_pids)" ]] && kill -0 "$pid" 2>/dev/null || break
  sleep 0.02
done
sleep "${AGRIBOT_SMOKE_STARTUP_SECONDS:-45}"
kill -0 "$pid"
ros2 topic list --no-daemon > "$ROOT/.validation/simulation_topics.txt"
timeout 20s ros2 topic echo /clock --once --no-daemon > "$ROOT/.validation/clock.txt"
echo 'Simulation started and published /clock. Inspect the log and topic list for sensor/localization status.'
