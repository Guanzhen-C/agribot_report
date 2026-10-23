#!/bin/sh

if [ -n "${ZSH_VERSION:-}" ]; then
    HPM_ENV_FILE="${(%):-%N}"
else
    HPM_ENV_FILE="${BASH_SOURCE[0]}"
fi

export HPM_WORKSPACE_ROOT="$(CDPATH= cd -- "$(dirname -- "$HPM_ENV_FILE")" && pwd)"
export HPM_SDK_BASE="$HPM_WORKSPACE_ROOT/.hpm/hpm_sdk"
export GNURISCV_TOOLCHAIN_PATH="$HPM_WORKSPACE_ROOT/.hpm/toolchain-shim"
export HPM_SDK_TOOLCHAIN_VARIANT=
export HPM_OPENOCD_HOME="$HPM_WORKSPACE_ROOT/.hpm/openocd"
export OPENOCD_SCRIPTS="$HPM_SDK_BASE/boards/openocd"
export HPM_JTAG_SPEED_KHZ="${HPM_JTAG_SPEED_KHZ:-8000}"
export PATH="$HPM_WORKSPACE_ROOT/.hpm/venv/bin:$GNURISCV_TOOLCHAIN_PATH/bin:$HPM_OPENOCD_HOME/bin:$PATH"

hpm_openocd() {
    "$HPM_OPENOCD_HOME/bin/openocd" \
        -s "$HPM_OPENOCD_HOME/share/openocd/scripts" \
        -s "$OPENOCD_SCRIPTS" \
        "$@"
}

unset HPM_ENV_FILE
