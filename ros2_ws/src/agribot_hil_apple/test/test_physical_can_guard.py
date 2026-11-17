import errno
from types import SimpleNamespace

import pytest
from agribot_hil_apple import physical_can_guard as module


def fixture(monkeypatch):
    now, packets, actions = [10.], [100], []
    monkeypatch.setattr(module.time, 'monotonic', lambda: now[0])
    monkeypatch.setattr(module.time, 'sleep', lambda _: None)
    monkeypatch.setattr(module.PhysicalTxGuard, 'packets', lambda _: packets[0])
    monkeypatch.setattr(module, 'verify_adapter', lambda *args: actions.append(('verify', args)))
    monkeypatch.setattr(module.subprocess, 'run', lambda cmd, **kwargs: actions.append(cmd))
    guard = module.PhysicalTxGuard('can0', 'bound-serial', 250000)
    return guard, now, packets, actions


def test_idle_never_stalls_but_missing_tx_completions_latch_between_polls(monkeypatch):
    guard, now, packets, _ = fixture(monkeypatch)
    guard.check()
    now[0] += 10.; guard.check()
    guard.sent(); now[0] += .31
    with pytest.raises(OSError, match='TX completions stalled'):
        guard.check()
    now[0] += .01
    with pytest.raises(OSError):
        guard.check()
    packets[0] += 1; now[0] += .11; guard.check()
    assert not guard.stalled and guard.last_progress_at == now[0]


def test_recovery_is_serial_checked_closes_old_socket_and_requires_new_tx_proof(monkeypatch):
    guard, now, packets, actions = fixture(monkeypatch)
    old = SimpleNamespace(close=lambda: actions.append('closed'))
    new = object()
    bus, recovered = guard.recover(old, lambda: actions.append('opened') or new)
    assert recovered and bus is new and guard.recoveries == 1
    assert actions == [('verify', ('can0', 'bound-serial', 250000)), 'closed',
        ['sudo', '-n', '/usr/local/libexec/agribot-hil-bus', 'pc', '250000'], 'opened']
    assert guard.last_progress_at == -float('inf')
    guard.sent(); packets[0] += 1; now[0] += .1; guard.check()
    assert guard.last_progress_at == now[0]


def test_recovery_is_rate_limited_bounded_and_refuses_identity_failure(monkeypatch):
    guard, now, _, actions = fixture(monkeypatch)
    bus = SimpleNamespace(close=lambda: actions.append('closed'))
    guard.recoveries = 3
    with pytest.raises(RuntimeError, match='Repeated physical CAN stalls'):
        guard.recover(bus, lambda: bus)
    assert 'closed' not in actions
    assert guard.recover(bus, lambda: bus) == (bus, False)
    now[0] += 6.
    def mismatch(*args):
        raise ValueError('wrong identity')
    monkeypatch.setattr(module, 'verify_adapter', mismatch)
    with pytest.raises(ValueError, match='wrong identity'):
        guard.recover(bus, lambda: bus)
    assert 'closed' not in actions


def test_progressing_physical_transmitter_is_not_reopened(monkeypatch):
    guard, _, _, actions = fixture(monkeypatch)
    guard.packets = iter([100, 101]).__next__
    bus = object()
    assert guard.recover(bus, lambda: pytest.fail('Must not reopen')) == (bus, False)
    assert actions == [('verify', ('can0', 'bound-serial', 250000))]


@pytest.mark.parametrize('interface,bitrate', [('can1', 250000), ('vcan73', 250000), ('can0', 500000)])
def test_unreviewed_interface_and_bitrate_never_reach_usb_or_helper(interface, bitrate):
    with pytest.raises(ValueError, match='restricted'):
        module.verify_adapter(interface, 'test', bitrate)


def test_only_temporary_socket_backpressure_is_recoverable():
    assert module.temporary_tx_error(OSError(errno.ENOBUFS, 'full'))
    assert module.temporary_tx_error(OSError(errno.EAGAIN, 'busy'))
    assert not module.temporary_tx_error(OSError(errno.ENETDOWN, 'offline'))
    assert not module.temporary_tx_error(ValueError('bad protocol'))
