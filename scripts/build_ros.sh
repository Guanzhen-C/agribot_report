#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
profile="${1:-simulation}"
if [[ $# -gt 0 ]]; then shift; fi
case "$profile" in
  simulation) skip=(--packages-ignore hikrobot_mvs_ros2) ;;
  hardware) skip=() ;;
  *) echo '用法：build_ros.sh simulation|hardware [colcon参数]' >&2; exit 2 ;;
esac
if [[ ! -f /opt/ros/humble/setup.bash ]]; then
  echo '需要 Ubuntu 22.04 / ROS 2 Humble，详见 docs/03_installation.md' >&2
  exit 2
fi
exec bash "$ROOT/ros2_ws/build_ros2_humble.sh" "${skip[@]}" "$@"
