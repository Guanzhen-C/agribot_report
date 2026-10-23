#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE="$ROOT/ros2_ws/src/agribot_mobile_app"
cd "$PACKAGE/web"
npm ci
npm run build
if [[ -f "$ROOT/ros2_ws/install/agribot_mobile_app/share/agribot_mobile_app/package.sh" ]]; then
  bash "$ROOT/scripts/build_ros.sh" simulation --packages-select agribot_mobile_app
fi
if [[ "${1:-web}" == android ]]; then
  : "${ANDROID_HOME:?请设置 Android SDK 目录 ANDROID_HOME}"
  rsync -a --delete --exclude downloads dist/ "$PACKAGE/android/app/src/main/assets/web/"
  cd "$PACKAGE/android"
  ./gradlew test lint assembleDebug
fi
