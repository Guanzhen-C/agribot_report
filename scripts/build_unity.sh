#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
set +u
source "$ROOT/scripts/env.sh"
set -u
UNITY_EDITOR="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/6000.0.26f1/Editor/Unity}"
if [[ ! -x "$UNITY_EDITOR" ]]; then
  echo '请安装 Unity 6000.0.26f1 Linux Editor 并激活许可证，或设置 UNITY_EDITOR。' >&2; exit 2
fi
python3 "$ROOT/tools/doctor.py" --unity
export AGRIBOT_STANDALONE_OUTPUT="${AGRIBOT_STANDALONE_OUTPUT:-$ROOT/dist/unity}"
export AGRIBOT_SKIP_DESKTOP_INSTALL=1
mkdir -p "$ROOT/.validation"
exec "$UNITY_EDITOR" -batchmode -force-glcore -quit -projectPath "$ROOT/unity" \
  -executeMethod Agribot.VehicleConfiguration.Editor.AgribotStandaloneBuilder.BuildFromCommandLine \
  -logFile "$ROOT/.validation/unity_build.log"
