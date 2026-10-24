#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python3 "$ROOT/tools/doctor.py" --unity
exec python3 "$ROOT/tools/install_prebuilt.py"
