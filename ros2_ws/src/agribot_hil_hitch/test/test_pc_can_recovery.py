from collections import deque
import errno
import importlib.util
import io
import math
from pathlib import Path
import sys
from types import SimpleNamespace

import pytest

SCRIPTS = Path(__file__).parents[1]/'scripts'
sys.path.insert(0, str(SCRIPTS))
spec = importlib.util.spec_from_file_location('hil_hitch_pc_recovery_test', SCRIPTS/'pc_bridge.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
from wire import drive_command


def fixture(monkeypatch):
    now, incoming, frames, drive, valve, power, failure = [10.], deque(), [], [], [], [], [None]
    def receive(_):
        if incoming:
            return incoming.popleft()
        raise BlockingIOError()
    def send(raw):
        if failure[0]:
            error, failure[0] = failure[0], None
            raise error
        frames.append(raw)
    bus = SimpleNamespace(recv=receive, send=send)
    guard = SimpleNamespace(check=lambda: None, sent=lambda: None, last_progress_at=-math.inf, recoveries=0)
    logger = SimpleNamespace(info=lambda *args, **kwargs: None, error=lambda *args, **kwargs: None)
    node = SimpleNamespace(bus=bus, tx_guard=guard, open_bus=lambda: bus, fault='', fault_since=0.,
        errors=0, recoverable_fault=False, awaiting_neutral=False, transmitting=False,
        last_feedback_sent=9.9, last_status=10., state=[0.]*9, state_at=10., steering=0.,
        drive_lease=module.Lease(), valve_lease=module.Lease(), drive=(.3, .1, True), valve=400,
        counter=0, power_pair=module.ActuatorPair(), power_state=None, power_at=-math.inf,
        trace=io.StringIO(), get_logger=lambda: logger,
        drive_pub=SimpleNamespace(publish=lambda msg: drive.append(msg.linear.x)),
        valve_pub=SimpleNamespace(publish=lambda msg: valve.append(msg.data)),
        power_pub=SimpleNamespace(publish=lambda msg: power.append(list(msg.data))),
        status_pub=SimpleNamespace(publish=lambda msg: None))
    for name in ('send', 'hold', 'tick_io'):
        setattr(node, name, lambda *args, method=name: getattr(module.Bridge, method)(node, *args))
    monkeypatch.setattr(module.time, 'monotonic', lambda: now[0])
    monkeypatch.setattr(module.time, 'sleep', lambda _: None)
    return node, now, incoming, frames, drive, valve, power, failure


def test_tx_backpressure_holds_drive_hitch_and_transmission_until_actual_progress(monkeypatch):
    node, now, _, frames, drive, valve, power, failure = fixture(monkeypatch)
    failure[0] = OSError(errno.EAGAIN, 'busy')
    module.Bridge.tick(node)
    assert node.fault and node.recoverable_fault
    assert drive[-1] == 0. and valve[-1] == 0 and power[-1][-2:] == [0., 1.]
    now[0] += .1; node.state_at = now[0]
    module.Bridge.tick(node)
    assert frames and node.fault
    now[0] += .1; node.state_at = now[0]; node.tx_guard.last_progress_at = now[0]
    module.Bridge.tick(node)
    assert not node.fault and node.drive == (0., 0., False) and node.valve == 0


def test_recovery_discards_all_cached_actuators_and_waits_for_new_disabled_drive(monkeypatch):
    node, now, incoming, frames, drive, valve, power, _ = fixture(monkeypatch)
    def stalled():
        raise OSError(errno.ENOBUFS, 'stalled')
    node.tx_guard.check = stalled
    node.fault, node.fault_since, node.recoverable_fault = 'stalled', 8., True
    old_pair = node.power_pair
    def recover(bus, opener):
        assert drive[-1] == 0. and valve[-1] == 0 and power[-1][-1] == 1.
        node.tx_guard.recoveries += 1
        return bus, True
    node.tx_guard.recover = recover
    module.Bridge.tick(node)
    assert node.awaiting_neutral and node.drive == (0., 0., False) and node.valve == 0
    assert node.power_pair is not old_pair and node.power_pair.current(now[0])[-1] == 1.
    node.tx_guard.check = lambda: None
    now[0] += .1; node.state_at = now[0]
    incoming.append(module.FRAME.pack(module.DRIVE, 8, drive_command(.3, 0., True, 1)))
    module.Bridge.tick(node)
    assert node.awaiting_neutral and frames
    assert drive[-1] == 0. and valve[-1] == 0 and power[-1][-1] == 1.
    incoming.append(module.FRAME.pack(module.DRIVE, 8, drive_command(0., 0., False, 2)))
    now[0] += .05; node.state_at = now[0]
    module.Bridge.tick(node)
    assert not node.awaiting_neutral and frames and node.fault


def test_fresh_measurements_continue_while_waiting_for_neutral_without_enabling_actuators(monkeypatch):
    node, _, _, frames, drive, valve, power, _ = fixture(monkeypatch)
    node.awaiting_neutral, node.fault, node.recoverable_fault = True, 'awaiting recovery', True
    node.fault_since, node.tx_guard.last_progress_at = 9., 10.
    module.Bridge.tick(node)
    assert frames and node.fault and node.awaiting_neutral
    assert drive[-1] == 0. and valve[-1] == 0 and power[-1][-1] == 1.
    identity, _, payload = module.FRAME.unpack(frames[0])
    assert identity == module.SENSOR and payload[5] == 0


def test_rdk_feedback_recovery_requires_new_neutral_priming_before_resuming():
    spec = importlib.util.spec_from_file_location('hil_hitch_client_recovery_test', SCRIPTS/'rdk_client.py')
    client = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(client)
    node = SimpleNamespace(powertrain_enabled=True, power_data={}, prime_until=0.)
    client.Client.update_feedback_priming(node, 10.)
    assert node.prime_until == 10.3
    from wire import seal
    keys = ('powertrain_engine', 'powertrain_transmission', 'powertrain_vehicle', 'powertrain_status')
    node.power_data = {key: dict(at=10.1, data=seal(bytes((0x22, 0, 0, 0, 0, 0, 1))).hex()) for key in keys}
    client.Client.update_feedback_priming(node, 10.1)
    assert node.prime_until == 10.3
    client.Client.update_feedback_priming(node, 10.5)
    assert node.prime_until == 10.8


def test_real_interface_failure_is_not_swallowed_as_queue_backpressure(monkeypatch):
    node, _, _, _, drive, valve, power, failure = fixture(monkeypatch)
    failure[0] = OSError(errno.ENETDOWN, 'offline')
    with pytest.raises(OSError, match='offline'):
        module.Bridge.tick(node)
    assert drive[-1] == 0. and valve[-1] == 0 and power[-1][-1] == 1.
