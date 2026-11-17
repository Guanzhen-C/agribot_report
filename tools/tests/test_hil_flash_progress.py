import os
from pathlib import Path
import sys

import pytest

sys.path.insert(0, str(Path(__file__).parents[1]/'hil'))
from flash_output import run_flash
import cli


def test_flash_progress_follows_tool_output_and_does_not_claim_reset_finished(tmp_path):
    events = []
    script = 'import sys,time\n'
    for line in ('Target successfully examined', 'Programming Started', 'Programming Finished', 'Verify Started', 'Verified OK'):
        script += 'print('+repr(line)+',flush=True);time.sleep(.01)\n'
    result = run_flash([sys.executable, '-c', script], dict(os.environ), tmp_path/'flash.log',
                       lambda amount, label: events.append((amount, label)))
    assert result.returncode == 0
    assert [amount for amount, _ in events] == [.15, .25, .7, .75, .95]
    assert 'Verified OK' in (tmp_path/'flash.log').read_text()


def test_failed_flash_process_is_not_reported_as_success(tmp_path):
    events = []
    result = run_flash([sys.executable, '-c', 'print("Programming Started",flush=True);raise SystemExit(3)'],
        dict(os.environ), tmp_path/'flash.log', lambda amount, label: events.append(amount))
    assert result.returncode == 3
    assert events == [.25]


def test_flash_timeout_also_covers_processes_that_close_their_output_pipe(tmp_path):
    script = 'import os,time;os.close(1);os.close(2);time.sleep(10)'
    with pytest.raises(TimeoutError, match='超时'):
        run_flash([sys.executable, '-c', script], dict(os.environ), tmp_path/'flash.log',
                  lambda *_: None, timeout=.1)


@pytest.mark.parametrize('algorithm,sensors', [
    (None, ['Radar', 'RTK', 'Camera', 'Imu']),
    ('wrong', ['Radar', 'RTK', 'Camera', 'Imu']),
    ('stvl_off_fastlivo_rtk_smac_hybrid_mppi', ['Radar', 'RTK', 'Camera']),
    ('stvl_off_fastlivo_rtk_smac_hybrid_mppi', None)])
def test_start_gate_rejects_missing_selections_before_any_hardware_or_file_action(algorithm, sensors, monkeypatch):
    monkeypatch.setattr(cli, 'firmware', lambda *_: pytest.fail('Hardware preflight must not be reached'))
    with pytest.raises(ValueError):
        cli.queue_start('tracked_sprayer', algorithm, sensors)
