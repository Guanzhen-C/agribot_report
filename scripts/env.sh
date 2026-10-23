#!/usr/bin/env bash
# Source this file; it does not start ROS nodes or enable vehicle output.
AGRIBOT_REPORT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export AGRIBOT_REPORT_ROOT
export AGRIBOT_ROS_WORKSPACE="$AGRIBOT_REPORT_ROOT/ros2_ws"
export AGRIBOT_WORKSPACE_SRC="$AGRIBOT_ROS_WORKSPACE/src"
export AGRIBOT_ROS_SYNC_SCRIPT="$AGRIBOT_WORKSPACE_SRC/agribot_vehicle_description/scripts/unity_sync_and_simulate.sh"
export AGRIBOT_SCANNED_SCENE_ROOT="${AGRIBOT_SCANNED_SCENE_ROOT:-$HOME/.local/share/agribot/scanned_scenes}"
_agribot_restore_nounset=0
case "$-" in *u*) _agribot_restore_nounset=1; set +u ;; esac
if [[ -f /opt/ros/humble/setup.bash ]]; then
  source /opt/ros/humble/setup.bash
fi
if [[ -f "$AGRIBOT_ROS_WORKSPACE/install/setup.bash" ]]; then
  source "$AGRIBOT_ROS_WORKSPACE/install/setup.bash"
fi
if [[ "$_agribot_restore_nounset" == 1 ]]; then set -u; fi
unset _agribot_restore_nounset
