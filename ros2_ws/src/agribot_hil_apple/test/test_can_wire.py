from functools import reduce
from operator import xor
import struct
from types import SimpleNamespace
import pytest

from agribot_hil_apple.can_wire import CAN_FRAME, decode_command, feedback_payload, int16
from agribot_hil_apple import can_wire


def test_socketcan_filter_does_not_select_error_only_list(monkeypatch):
    class FakeSocket:
        def setsockopt(self, level, option, value):
            self.filters = value

        def bind(self, address):
            pass

        def settimeout(self, value):
            pass

        def close(self):
            pass

    sock = FakeSocket()
    monkeypatch.setattr(can_wire, 'check_usb_can', lambda *args: None)
    monkeypatch.setattr(can_wire.socket, 'socket', lambda *args: sock)
    monkeypatch.setattr(can_wire.CanSocket, 'transmitted_frames', lambda _: 100)
    bus = can_wire.CanSocket('can0', 'test', [0x181])
    identifier, mask = struct.unpack('=II', sock.filters)
    assert identifier == 0x181
    assert mask == 0xc00007ff
    assert not mask & 0x20000000
    bus.close()


def test_physical_tx_progress_watchdog_detects_silent_stall_and_ignores_idle(monkeypatch):
    bus = object.__new__(can_wire.CanSocket)
    now, packets = [1.], [100]
    bus._last_tx_packets, bus._tx_pending_since, bus._tx_checked_at = 100, None, 0.
    bus._tx_stalled = False
    bus.transmitted_frames = lambda: packets[0]
    monkeypatch.setattr(can_wire.time, 'monotonic', lambda: now[0])
    bus.check_transmitter()  # No sends: an idle interface is not a stall.
    bus._tx_pending_since = 1.
    now[0] = 1.2
    bus.check_transmitter()
    now[0] = 1.31
    with pytest.raises(OSError, match='physical TX completions stalled'):
        bus.check_transmitter()
    now[0] += .01
    with pytest.raises(OSError, match='physical TX completions stalled'):
        bus.check_transmitter()  # A throttled poll must not falsely clear the stall.
    packets[0] += 3
    now[0] = 1.5
    bus.check_transmitter()
    assert bus._tx_pending_since is None
    now[0] = 5.
    bus.check_transmitter()


def test_ackermann_classic_frame_and_endianness():
    assert CAN_FRAME.size == 16
    assert decode_command(bytes.fromhex('01900000ff060000')) == (.4, -.25)
    for invalid in (b'', bytes.fromhex('0000010000000000'), b'\0' * 9):
        with pytest.raises(ValueError):
            decode_command(invalid)


def test_telemetry_matches_physical_decoder_layout():
    payload = feedback_payload(.4, -.3, voltage=24.123, stopped=True)
    assert len(payload) == 24 and payload[:2] == b'\x7b\x01' and payload[-1] == 0x7d
    assert struct.unpack_from('>hhh', payload, 2) == (400, 0, -300)
    assert struct.unpack_from('>H', payload, 20)[0] == 24123
    assert payload[22] == reduce(xor, payload[:22], 0)
    assert int16(.5) == 1 and int16(-.5) == -1
    assert int16(1e6) == 32767 and int16(-1e6) == -32768
    with pytest.raises(ValueError):
        feedback_payload(voltage=float('nan'))


def recovery_fixture(monkeypatch, counts):
    events = []
    bus = object.__new__(can_wire.CanSocket)
    bus.interface, bus.serial = 'can0', 'test'
    bus.transmitted_frames = iter(counts).__next__
    bus.close = lambda: events.append('closed')
    bus.open = lambda: events.append('opened')
    bus.socket = SimpleNamespace(setblocking=lambda value: events.append(('blocking', value)))
    monkeypatch.setattr(can_wire, 'check_usb_can', lambda *args: events.append('identity-checked'))
    monkeypatch.setattr(can_wire.time, 'sleep', lambda _: None)
    monkeypatch.setattr(can_wire.subprocess, 'run', lambda command, **kwargs: events.append(command))
    return bus, events


def test_stalled_transport_is_closed_before_only_the_serial_bound_helper_runs(monkeypatch):
    bus, events = recovery_fixture(monkeypatch, [10, 10])
    assert bus.recover_stalled_transmitter() is True
    assert events == ['identity-checked', 'closed',
        ['sudo', '-n', '/usr/local/libexec/agribot-hil-bus', 'pc', '1000000'],
        'opened', ('blocking', False)]


def test_transmitter_that_is_draining_is_not_reset(monkeypatch):
    bus, events = recovery_fixture(monkeypatch, [10, 11])
    assert bus.recover_stalled_transmitter() is False
    assert events == ['identity-checked']


def test_other_interface_or_failed_identity_never_reaches_reset_helper(monkeypatch):
    bus, events = recovery_fixture(monkeypatch, [10, 10])
    bus.interface = 'can1'
    with pytest.raises(ValueError, match='restricted'):
        bus.recover_stalled_transmitter()
    assert events == []
    bus.interface = 'can0'
    def mismatch(*args):
        raise ValueError('wrong USB or CAN error state')
    monkeypatch.setattr(can_wire, 'check_usb_can', mismatch)
    with pytest.raises(ValueError, match='wrong USB'):
        bus.recover_stalled_transmitter()
    assert events == []
