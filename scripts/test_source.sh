#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/ros2_ws/src"
export PYTHONPATH="$SRC/agribot_farm_sim:$SRC/agribot_vehicle_config_tools:$SRC/agribot_k2_sim${PYTHONPATH:+:$PYTHONPATH}"
cd "$ROOT"
/usr/bin/python3 -m pytest -q \
  "$SRC/agribot_farm_sim/test/test_natural_recommendation.py" \
  "$SRC/agribot_farm_sim/test/test_scene_tools.py" \
  "$SRC/agribot_vehicle_config_tools/test/test_vehicle_config.py" \
  "$SRC/agribot_k2_sim/test/test_import_scene.py" \
  "$@"
