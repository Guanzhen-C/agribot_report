#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/scripts/env.sh"
exec "$AGRIBOT_VISION_PYTHON" "$ROOT/tools/validate_vision.py"
