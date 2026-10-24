#!/usr/bin/env bash
# Source this file; it does not start ROS nodes or enable vehicle output.
AGRIBOT_REPORT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export AGRIBOT_REPORT_ROOT
export AGRIBOT_ROS_WORKSPACE="$AGRIBOT_REPORT_ROOT/ros2_ws"
export AGRIBOT_WORKSPACE_SRC="$AGRIBOT_ROS_WORKSPACE/src"
export AGRIBOT_ROS_SYNC_SCRIPT="$AGRIBOT_WORKSPACE_SRC/agribot_vehicle_description/scripts/unity_sync_and_simulate.sh"
export AGRIBOT_SCANNED_SCENE_ROOT="${AGRIBOT_SCANNED_SCENE_ROOT:-$AGRIBOT_REPORT_ROOT/.local/scanned_scenes}"
export AGRIBOT_LLM_CONFIG="${AGRIBOT_LLM_CONFIG:-$AGRIBOT_REPORT_ROOT/.local/llm.json}"
export AGRIBOT_VISION_RUNTIME_ROOT="${AGRIBOT_VISION_RUNTIME_ROOT:-$AGRIBOT_REPORT_ROOT/.local/vision}"
export AGRIBOT_VISION_MODEL_DIR="${AGRIBOT_VISION_MODEL_DIR:-$AGRIBOT_REPORT_ROOT/resources/vision_models}"
export AGRIBOT_VISION_PYTHON="${AGRIBOT_VISION_PYTHON:-$AGRIBOT_VISION_RUNTIME_ROOT/vision_venv/bin/python3}"
export AGRIBOT_HPM_FIRMWARE_ROOT="${AGRIBOT_HPM_FIRMWARE_ROOT:-$AGRIBOT_REPORT_ROOT/resources/deployment/ackermann}"
export XDG_STATE_HOME="${AGRIBOT_REPORT_STATE_HOME:-$AGRIBOT_REPORT_ROOT/.local/state}"
export AGRIBOT_REPORT_CONFIG_HOME="${AGRIBOT_REPORT_CONFIG_HOME:-$AGRIBOT_REPORT_ROOT/.local/config}"
export ROS_DOMAIN_ID="${ROS_DOMAIN_ID:-37}"
export ROS_LOCALHOST_ONLY="${ROS_LOCALHOST_ONLY:-1}"
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
