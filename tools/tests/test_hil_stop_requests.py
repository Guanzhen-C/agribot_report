from pathlib import Path
from types import SimpleNamespace
import sys

sys.path.insert(0, str(Path(__file__).parents[1]/'hil'))
import cli


def test_stop_always_has_a_cleanup_owner_even_when_main_worker_is_still_active(tmp_path, monkeypatch):
    monkeypatch.setattr(cli, 'STATE', tmp_path)
    monkeypatch.setattr(cli, 'unit_active', lambda name=cli.UNIT: name == cli.UNIT)
    calls = []
    monkeypatch.setattr(cli.subprocess, 'run', lambda command, **_: calls.append(command) or
                        SimpleNamespace(returncode=0))
    cli.stop()
    assert (tmp_path/'stop.request').exists()
    assert len(calls) == 1
    assert '--unit='+cli.STOP_UNIT in calls[0]
    assert '--stop-only' in calls[0]


def test_repeated_stop_requests_share_one_cleanup_unit(tmp_path, monkeypatch):
    monkeypatch.setattr(cli, 'STATE', tmp_path)
    monkeypatch.setattr(cli, 'unit_active', lambda *_: True)
    monkeypatch.setattr(cli.subprocess, 'run', lambda *_a, **_k: (_ for _ in ()).throw(AssertionError('duplicate stop unit')))
    cli.stop()
    assert (tmp_path/'stop.request').exists()


def test_stop_waiter_cleans_up_after_main_worker_completes_without_reading_stop(tmp_path, monkeypatch):
    monkeypatch.setattr(cli, 'STATE', tmp_path)
    cli.save_status(dict(phase='running', profile='tractor_cvt', run_id='race-fixture', session_alive=True))
    states = iter([True, True, False])
    monkeypatch.setattr(cli, 'unit_active', lambda *_: next(states))
    events = []
    def wait(_):
        events.append('wait')
        cli.save_status(dict(phase='completed', profile='tractor_cvt', run_id='race-fixture', session_alive=True))
    monkeypatch.setattr(cli.time, 'sleep', wait)
    monkeypatch.setattr(cli, 'load', lambda **_: {})
    class Backend:
        def __init__(self, *_): pass
        def safe_stop(self): events.append('stopped')
    monkeypatch.setattr(cli, 'RuntimeBackend', Backend)
    cli.worker(None, None, stop_only=True)
    assert events == ['wait', 'wait', 'stopped']
    assert cli.read_status()['phase'] == 'stopped'
    assert cli.read_status()['session_alive'] is False


def test_stop_waiter_does_not_repeat_cleanup_when_main_worker_already_stopped(tmp_path, monkeypatch):
    monkeypatch.setattr(cli, 'STATE', tmp_path)
    cli.save_status(dict(phase='stopped', session_alive=False))
    states = iter([True, False])
    monkeypatch.setattr(cli, 'unit_active', lambda *_: next(states))
    monkeypatch.setattr(cli.time, 'sleep', lambda _: None)
    monkeypatch.setattr(cli, 'load', lambda **_: (_ for _ in ()).throw(AssertionError('already stopped')))
    cli.worker(None, None, stop_only=True)
