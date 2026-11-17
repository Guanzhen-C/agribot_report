from collections import deque
import errno
import math
from types import SimpleNamespace

import pytest
from agribot_hil_apple import tracked_gazebo_can_bridge as module
from agribot_hil_apple.tracked_work_wire import CounterLease, checked


def fixture(monkeypatch):
    now, incoming, frames, drive, work, failure = [10.], deque(), [], [], [], [None]
    def receive(_):
        if incoming:
            return incoming.popleft()
        raise BlockingIOError()
    def send(frame):
        if failure[0]:
            error, failure[0] = failure[0], None
            raise error
        frames.append(frame)
    bus = SimpleNamespace(recv=receive, send=send)
    guard = SimpleNamespace(check=lambda: None, sent=lambda: None,
        last_progress_at=-math.inf, recoveries=0)
    logger = SimpleNamespace(info=lambda *args, **kwargs: None, error=lambda *args, **kwargs: None)
    node = SimpleNamespace(bus=bus, tx_guard=guard, open_bus=lambda: bus,
        motion=True, work=True, implement='sprayer', drive_lease=CounterLease(.5), work_lease=CounterLease(.5),
        target=(.3, 0., False), gears=(3, 3, 0), speed=.2, yaw=0., rpm=[100., 100.],
        truth_at=10., motors_at=10., counter=0, rx=0, tx=0, rejected=0,
        last_feedback=0., last_work_feedback=0., last_status=10., last_feedback_sent=9.9,
        fault='', recoverable_fault=False, awaiting_neutral=False, fault_since=0., errors=0,
        drive_pub=SimpleNamespace(publish=lambda msg: drive.append(msg.linear.x)),
        work_pub=SimpleNamespace(publish=lambda msg: work.append(list(msg.data))),
        status=SimpleNamespace(publish=lambda msg: None), get_logger=lambda: logger)
    node.send = lambda *args: module.TrackedGazeboCanBridge.send(node, *args)
    node.hold = lambda: module.TrackedGazeboCanBridge.hold(node)
    monkeypatch.setattr(module.time, 'monotonic', lambda: now[0])
    monkeypatch.setattr(module.time, 'sleep', lambda _: None)
    return node, now, incoming, frames, drive, work, failure


def test_short_send_error_holds_both_actuators_and_requires_physical_tx_progress(monkeypatch):
    node, now, _, frames, drive, work, failure = fixture(monkeypatch)
    failure[0] = OSError(errno.EAGAIN, 'busy')
    module.TrackedGazeboCanBridge.tick(node)
    assert node.recoverable_fault and drive[-1] == 0. and work[-1] == [0., 0.]
    now[0] += .2
    module.TrackedGazeboCanBridge.tick(node)
    assert frames and node.fault  # Enqueue success alone must not clear it.
    node.tx_guard.last_progress_at = 10.25; now[0] = 10.4
    module.TrackedGazeboCanBridge.tick(node)
    assert not node.fault and node.target == (0., 0., False) and node.gears == (0, 0, 0)


def test_reopen_waits_for_fresh_zero_and_never_replays_old_nonzero_work(monkeypatch):
    node, now, incoming, frames, drive, work, _ = fixture(monkeypatch)
    def stalled():
        raise OSError(errno.ENOBUFS, 'TX stalled')
    node.tx_guard.check = stalled
    node.fault, node.recoverable_fault, node.fault_since = 'TX stalled', True, 8.
    def recover(bus, opener):
        assert drive[-1] == 0. and work[-1] == [0., 0.]
        node.tx_guard.recoveries += 1
        return bus, True
    node.tx_guard.recover = recover
    module.TrackedGazeboCanBridge.tick(node)
    assert node.awaiting_neutral and node.target == (0., 0., False) and node.gears == (0, 0, 0)
    node.tx_guard.check = lambda: None
    now[0] += .1; node.truth_at = node.motors_at = now[0]
    incoming.append(module.CAN_FRAME.pack(0x514, 8, checked([0, 10, 10, 0, 0, 0, 0, 0], 1)))
    module.TrackedGazeboCanBridge.tick(node)
    assert node.awaiting_neutral and not frames and drive[-1] == 0.
    now[0] += .1
    incoming.append(module.CAN_FRAME.pack(0x514, 8, checked(bytes(8), 2)))
    module.TrackedGazeboCanBridge.tick(node)
    assert not node.awaiting_neutral and frames and node.fault
    now[0] += .2; node.truth_at = node.motors_at = now[0]
    node.tx_guard.last_progress_at = now[0]
    module.TrackedGazeboCanBridge.tick(node)
    assert not node.fault and node.target == (0., 0., False)


def test_real_bus_failure_stays_latched_and_is_never_downgraded_by_queue_full(monkeypatch):
    node, now, _, _, drive, work, failure = fixture(monkeypatch)
    failure[0] = OSError(errno.ENETDOWN, 'offline')
    module.TrackedGazeboCanBridge.tick(node)
    first = node.fault
    assert first and not node.recoverable_fault
    failure[0] = OSError(errno.ENOBUFS, 'later full')
    now[0] += .2
    module.TrackedGazeboCanBridge.tick(node)
    assert node.fault == first and not node.recoverable_fault
    assert drive[-1] == 0. and work[-1] == [0., 0.]
