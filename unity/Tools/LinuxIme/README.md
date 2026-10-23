# Linux IME Commit Bridge

The current Unity 6000.0 Linux player truncates Fcitx `CommitString` messages to
their first character before they reach TMP or Input System. A real Fcitx Pinyin
`nihao` + Space reproduces this: the native payload is six UTF-8 bytes, but the
field displays only the first Chinese character. Injecting C# text events alone
does not reproduce this native failure.

The Linux standalone builder compiles a small process-local shim and preloads it
from `start_agribot_configurator.sh`, used by the desktop shortcut. It preserves
the complete Fcitx UTF-8 commit and suppresses Unity's truncated copy only while
the App's editable field is focused. `DesktopTextInputBridge` inserts the commit
atomically, preserving the selection, caret, character limit and surrogate
pairs. Ordinary keys, clipboard commands and non-Fcitx traffic use the normal
input path. The native queue is bounded and cleared on focus loss or disable.

The assistant's `AssistantInputField` sends on Enter (including the numeric
keypad) and inserts a newline on Shift+Enter without dropping focus. It ignores
an Enter that confirms an IME candidate, including commits rejected at the
character limit, and suppresses the redundant newline from the keyboard
fallback. The send button and keyboard use the same guarded query handler.

The runtime removes this shim from `LD_PRELOAD` for child processes. It must not
be injected into ROS, Gazebo or the Python assistant. Nothing is installed into
system libraries, and no other application's input is monitored. Normal builds
do not log input text. `AGRIBOT_IME_DIAGNOSTICS=1` prints only commit lengths.

Use the desktop shortcut or the generated launcher, not the bare player binary,
to activate the workaround. Other platforms and the Unity Editor retain the
managed keyboard fallback. This workaround targets the current x86-64 Linux
standalone/Fcitx setup; it is not a claim of universal IBus/Wayland support.

Build requirements: GCC, glibc, libdl and pthread (already present on the build
workstation). The shim uses libdbus symbols already loaded by Unity and does not
depend on a system-wide D-Bus configuration change.

Native checks:

```bash
gcc -D_GNU_SOURCE -Wall -Wextra -Werror -pthread \
  Tools/LinuxIme/test_linux_ime.c -ldl -o /tmp/agribot-ime-test
/tmp/agribot-ime-test
```

Also run the PlayMode Chinese commit/selection/limit tests and verify a built
player using actual Fcitx candidate selection. Do not replace that last check
with `InputSystem.QueueTextEvent` alone.
