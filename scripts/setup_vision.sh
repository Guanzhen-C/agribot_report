#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/scripts/env.sh"
exec bash "$ROOT/ros2_ws/src/agribot_visual_perception/scripts/setup_ai_runtime.sh"
