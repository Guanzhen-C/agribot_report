#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PLATFORM="${1:-EditMode}"
if [[ "$PLATFORM" != EditMode && "$PLATFORM" != PlayMode ]]; then
  echo '用法：scripts/test_unity.sh [EditMode|PlayMode] [Unity测试参数]' >&2
  exit 2
fi
if [[ $# -gt 0 ]]; then shift; fi
source "$ROOT/scripts/env.sh"
EDITOR="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/6000.0.26f1/Editor/Unity}"
if [[ ! -x "$EDITOR" ]]; then
  echo '需要已激活的 Unity 6000.0.26f1 Linux Editor；可设置 UNITY_EDITOR。' >&2
  exit 2
fi
mkdir -p "$ROOT/.validation"
exec "$EDITOR" -batchmode -force-glcore -job-worker-count 4 -projectPath "$ROOT/unity" \
  -runTests -testPlatform "$PLATFORM" \
  -testResults "$ROOT/.validation/unity_${PLATFORM}.xml" \
  -logFile "$ROOT/.validation/unity_${PLATFORM}.log" "$@"
