#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
install_system=true
build_unity=auto
vision=true
for argument in "$@"; do
  case "$argument" in
    --skip-system) install_system=false ;;
    --skip-unity-build) build_unity=false ;;
    --build-unity) build_unity=true ;;
    --skip-vision) vision=false ;;
    *) echo '用法：bootstrap.sh [--skip-system] [--build-unity|--skip-unity-build] [--skip-vision]' >&2; exit 2 ;;
  esac
done
if "$install_system"; then bash "$ROOT/scripts/install_dependencies.sh"; fi
if ! git lfs version >/dev/null 2>&1; then
  echo '请先安装git-lfs，或不要使用--skip-system。' >&2
  exit 2
fi
git -C "$ROOT" lfs install --local
git -C "$ROOT" lfs pull
python3 "$ROOT/tools/doctor.py" --unity --hashes
bash "$ROOT/scripts/build_ros.sh" simulation
if "$vision"; then bash "$ROOT/scripts/setup_vision.sh"; fi
if [[ "$build_unity" == auto && -f "$ROOT/prebuilt/manifest.json" ]]; then
  bash "$ROOT/scripts/install_app.sh"
elif [[ "$build_unity" != false ]]; then
  bash "$ROOT/scripts/build_unity.sh"
fi
printf '构建完成。启动：bash %q/scripts/run_unity.sh\n' "$ROOT"
