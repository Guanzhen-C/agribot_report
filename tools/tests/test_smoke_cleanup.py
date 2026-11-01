"""Exercise the real shell lifecycle with descendant processes in separate groups."""
import os
from pathlib import Path
import shutil
import subprocess

import pytest


ROOT = Path(__file__).resolve().parents[2]


@pytest.mark.parametrize("failure", ["none", "topics", "clock", "startup", "launch"])
def test_smoke_cleans_its_session_on_success_and_failure(tmp_path, failure):
    scripts = tmp_path / "scripts"
    scripts.mkdir()
    shutil.copy2(ROOT / "scripts/smoke_simulation.sh", scripts)
    (scripts / "env.sh").write_text(":\n")
    executable = scripts / "ros2"
    executable.write_text('''#!/usr/bin/python3
import os, signal, subprocess, sys, time
from pathlib import Path
if sys.argv[1] == 'launch':
    child = subprocess.Popen(['/usr/bin/python3', '-c',
        'import signal,time; signal.signal(signal.SIGINT, signal.SIG_IGN); time.sleep(120)'],
        preexec_fn=os.setpgrp)
    Path(os.environ['SMOKE_PIDS']).write_text(str(os.getpid())+' '+str(child.pid))
    if os.environ['SMOKE_FAILURE'] == 'launch':
        sys.exit(3)
    signal.signal(signal.SIGINT, lambda *_: sys.exit(0))
    time.sleep(120)
elif sys.argv[1:3] == ['topic', 'list']:
    print('/clock')
    sys.exit(2 if os.environ['SMOKE_FAILURE'] == 'topics' else 0)
else:
    print('clock: {sec: 1}')
    sys.exit(2 if os.environ['SMOKE_FAILURE'] == 'clock' else 0)
''')
    executable.chmod(0o755)
    pids = tmp_path / "pids"
    env = dict(os.environ, PATH=str(scripts) + os.pathsep + os.environ["PATH"],
               SMOKE_PIDS=str(pids), SMOKE_FAILURE=failure,
               AGRIBOT_SMOKE_STARTUP_SECONDS="invalid" if failure == "startup" else "1")
    result = subprocess.run(["bash", str(scripts / "smoke_simulation.sh")], env=env,
                            capture_output=True, text=True, timeout=35)
    assert (result.returncode == 0) == (failure == "none"), result.stderr
    if pids.exists():
        for pid in pids.read_text().split():
            stat = Path(f"/proc/{pid}/stat")
            assert not stat.exists() or stat.read_text().split(") ", 1)[1].startswith("Z"), pid
    assert ("Simulation started and published /clock." in result.stdout) == (failure == "none")
