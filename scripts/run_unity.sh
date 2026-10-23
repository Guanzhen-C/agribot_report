#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
set +u
source "$ROOT/scripts/env.sh"
set -u
launcher="${AGRIBOT_STANDALONE_OUTPUT:-$ROOT/dist/unity}/start_agribot_configurator.sh"
if [[ ! -x "$launcher" ]]; then
  echo '请先运行 scripts/build_unity.sh。' >&2; exit 2
fi
exec "$launcher" "$@"
