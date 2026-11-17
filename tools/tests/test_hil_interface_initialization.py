import importlib.util
from pathlib import Path

import pytest

SOURCE = Path(__file__).resolve().parents[2]/'docs/deliverables/20261116_hil_retest/interface_checks.py'
spec = importlib.util.spec_from_file_location('hil_interface_check', SOURCE)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
TRANSITION = 'HIL CAN timing/state is not safe for controlled recovery'


def test_positive_probe_waits_for_actual_safe_state_without_reconfiguration(monkeypatch):
    clock, calls, samples = [10.], [], []
    monkeypatch.setattr(module.time, 'monotonic', lambda: clock[0])
    monkeypatch.setattr(module.time, 'sleep', lambda seconds: clock.__setitem__(0, clock[0]+seconds))
    def verify():
        calls.append(clock[0])
        if len(calls) < 3:
            raise ValueError(TRANSITION)
    module.wait_for_verified_adapter(verify, lambda: {'observed': True}, samples)
    assert [s['ready'] for s in samples] == [False, False, True]
    assert calls[-1]-calls[0] == pytest.approx(.2)


def test_identity_error_is_never_retried_or_accepted():
    def verify():
        raise ValueError('HIL USB-CAN identity mismatch')
    samples = []
    with pytest.raises(ValueError, match='identity mismatch'):
        module.wait_for_verified_adapter(verify, lambda: {}, samples)
    assert len(samples) == 1 and not samples[0]['ready']


def test_unsafe_state_still_fails_at_bounded_initialization_deadline(monkeypatch):
    clock, samples = [10.], []
    monkeypatch.setattr(module.time, 'monotonic', lambda: clock[0])
    monkeypatch.setattr(module.time, 'sleep', lambda seconds: clock.__setitem__(0, clock[0]+seconds))
    def verify():
        raise ValueError(TRANSITION)
    with pytest.raises(ValueError, match='timing/state'):
        module.wait_for_verified_adapter(verify, lambda: {}, samples, timeout=.25)
    assert all(not s['ready'] for s in samples)
