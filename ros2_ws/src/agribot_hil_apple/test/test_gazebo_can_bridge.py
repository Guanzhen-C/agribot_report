import math
import struct
import errno
from collections import deque
from types import SimpleNamespace
import pytest
import agribot_hil_apple.gazebo_can_bridge as module
from agribot_hil_apple.gazebo_can_bridge import actuator_command, GazeboCanBridge


def test_native_steering_uses_ackermann_kinematics_and_authorization():
    payload = struct.pack('>hhhh', 300, 0, 200, 0)
    speed, yaw = actuator_command(payload, .5265874862670898, True)
    assert speed == .3
    assert yaw == pytest.approx(.3 * math.tan(.2) / .5265874862670898)
    assert actuator_command(payload, .5265874862670898, False) == (0., 0.)
    with pytest.raises(ValueError):
        actuator_command(struct.pack('>hhhh', 1000, 0, 0, 0), .526, True)


def bridge_fixture(monkeypatch):
    now, commands, published, feedback = [10.], deque(), [], []
    failing = [None]
    def send(identifier, payload):
        if failing[0] is not None:
            error, failing[0] = failing[0], None
            raise error
        feedback.append((identifier, payload))
    logger = SimpleNamespace(error=lambda *args, **kwargs: None, info=lambda *args, **kwargs: None)
    node = SimpleNamespace(authorized=True, wheelbase=.526, last_truth=10., last_command=10.,
        target=(.3, 0.), rx=0, tx=0, errors=0, nonzero_rx=0, last_feedback=0.,
        last_feedback_sent=9.9, last_status=10., speed=0., yaw_rate=0., fault='', recoverable_fault=False,
        last_recovery_attempt=-math.inf, transport_recoveries=0,
        awaiting_neutral=False, rejected_until_neutral=0,
        bus=SimpleNamespace(receive=lambda: commands.popleft() if commands else None, send=send,
                            check_transmitter=lambda: None),
        publisher=SimpleNamespace(publish=lambda msg: published.append((msg.linear.x, msg.angular.z))),
        status=SimpleNamespace(publish=lambda msg: None), get_logger=lambda: logger)
    monkeypatch.setattr(module.time, 'monotonic', lambda: now[0])
    return node, now, commands, published, feedback, failing


@pytest.mark.parametrize('code', [errno.ENOBUFS, errno.EAGAIN])
def test_transient_tx_backpressure_stops_then_recovers_only_after_feedback_and_new_command(monkeypatch, code):
    node, now, commands, published, feedback, failing = bridge_fixture(monkeypatch)
    failing[0] = OSError(code, 'temporary TX backpressure')
    GazeboCanBridge.tick(node)
    assert node.fault and node.recoverable_fault
    assert published[-1] == (0., 0.)
    assert not feedback
    # No high-rate retries within the 50 ms feedback interval.
    now[0] += .01
    GazeboCanBridge.tick(node)
    assert not feedback and published[-1] == (0., 0.)
    now[0] += .05
    GazeboCanBridge.tick(node)
    assert len(feedback) == 3
    assert feedback[0][1][:2] == b'\x7b\x01'
    assert not node.fault and not node.recoverable_fault
    assert node.last_command == 0. and node.target == (0., 0.)
    assert published[-1] == (0., 0.)
    now[0] += .01
    GazeboCanBridge.tick(node)
    assert published[-1] == (0., 0.)
    commands.append((module.COMMAND_ID, struct.pack('>hhhh', 300, 0, 200, 0)))
    now[0] += .01
    GazeboCanBridge.tick(node)
    assert published[-1] == (0., 0.) and node.awaiting_neutral
    commands.append((module.COMMAND_ID, bytes(8)))
    now[0] += .01
    GazeboCanBridge.tick(node)
    assert published[-1] == (0., 0.) and not node.awaiting_neutral
    commands.append((module.COMMAND_ID, struct.pack('>hhhh', 300, 0, 200, 0)))
    now[0] += .01
    GazeboCanBridge.tick(node)
    assert published[-1][0] == .3


def test_expired_native_stream_rejects_queued_positive_until_explicit_zero(monkeypatch):
    node, now, commands, published, _, _ = bridge_fixture(monkeypatch)
    now[0] += .31
    node.last_truth = node.last_feedback_sent = now[0]
    GazeboCanBridge.tick(node)
    assert node.awaiting_neutral and published[-1] == (0., 0.)
    commands.append((module.COMMAND_ID, struct.pack('>hhhh', 120, 0, 0, 0)))
    now[0] += .01
    GazeboCanBridge.tick(node)
    assert published[-1] == (0., 0.) and node.rejected_until_neutral == 1
    assert node.last_command == 10.
    commands.append((module.COMMAND_ID, bytes(8)))
    now[0] += .01
    GazeboCanBridge.tick(node)
    assert not node.awaiting_neutral and published[-1] == (0., 0.)
    commands.append((module.COMMAND_ID, struct.pack('>hhhh', 120, 0, 0, 0)))
    now[0] += .01
    GazeboCanBridge.tick(node)
    assert published[-1] == (.12, 0.)


def test_resume_batch_cannot_execute_old_positive_before_its_zero(monkeypatch):
    node, now, commands, published, _, _ = bridge_fixture(monkeypatch)
    now[0] += 1.5
    node.last_truth = node.last_feedback_sent = now[0]
    commands.extend([(module.COMMAND_ID, struct.pack('>hhhh', 120, 0, 0, 0)),
                     (module.COMMAND_ID, bytes(8))])
    GazeboCanBridge.tick(node)
    assert node.rejected_until_neutral == 1
    assert not node.awaiting_neutral and published[-1] == (0., 0.)


def test_partial_feedback_failure_is_rate_limited_too(monkeypatch):
    node, now, _, published, feedback, _ = bridge_fixture(monkeypatch)
    def send(identifier, payload):
        feedback.append((identifier, payload))
        if len(feedback) == 2:
            raise OSError(errno.ENOBUFS, 'second frame rejected')
    node.bus.send = send
    GazeboCanBridge.tick(node)
    assert node.tx == 1 and node.recoverable_fault
    now[0] += .01
    GazeboCanBridge.tick(node)
    assert len(feedback) == 2 and published[-1] == (0., 0.)


def test_real_transport_fault_never_auto_recovers(monkeypatch):
    node, now, _, published, feedback, failing = bridge_fixture(monkeypatch)
    failing[0] = OSError(errno.ENETDOWN, 'interface offline')
    GazeboCanBridge.tick(node)
    fault = node.fault
    assert fault and not node.recoverable_fault
    failing[0] = OSError(errno.ENOBUFS, 'later temporary error')
    now[0] += .06
    GazeboCanBridge.tick(node)
    assert node.fault == fault and not node.recoverable_fault
    now[0] += .06
    GazeboCanBridge.tick(node)
    assert len(feedback) == 3 and node.fault == fault
    assert published[-1] == (0., 0.)


def test_cold_start_and_invalid_native_command_cannot_move_vehicle(monkeypatch):
    node, now, commands, published, _, _ = bridge_fixture(monkeypatch)
    node.last_feedback_sent = 0.
    GazeboCanBridge.tick(node)
    assert published[-1] == (0., 0.)
    commands.append((module.COMMAND_ID, struct.pack('>hhhh', 1000, 0, 0, 0)))
    now[0] += .06
    GazeboCanBridge.tick(node)
    assert node.fault and not node.recoverable_fault
    commands.append((module.COMMAND_ID, struct.pack('>hhhh', 300, 0, 0, 0)))
    now[0] += .06
    GazeboCanBridge.tick(node)
    assert node.fault and published[-1] == (0., 0.)


def test_feedback_fragments_keep_native_bytes_but_are_paced_for_usb_bridge(monkeypatch):
    node, _, _, _, feedback, _ = bridge_fixture(monkeypatch)
    intervals = []
    monkeypatch.setattr(module.time, 'sleep', intervals.append)
    GazeboCanBridge.tick(node)
    assert intervals == [.003, .003]
    assert [ident for ident, _ in feedback] == list(module.FEEDBACK_IDS)
    assert b''.join(payload for _, payload in feedback) == module.feedback_payload(stopped=False)


def test_persistent_transmit_stall_reopens_only_while_stopped_and_drops_old_target(monkeypatch):
    node, now, _, published, _, failing = bridge_fixture(monkeypatch)
    resets = []
    def recover():
        assert published[-1] == (0., 0.)
        assert node.target == (0., 0.) and node.last_command == 0.
        resets.append(True)
        return True
    node.bus.recover_stalled_transmitter = recover
    node.last_feedback_sent = 8.
    failing[0] = OSError(errno.ENOBUFS, 'driver TX queue stalled')
    GazeboCanBridge.tick(node)
    assert resets == [True] and node.transport_recoveries == 1
    assert node.fault and node.recoverable_fault
    assert node.last_command == node.last_feedback_sent == 0.
    # Even another failed send cannot cause an immediate reset loop.
    now[0] += .1; node.last_command = now[0]
    failing[0] = OSError(errno.ENOBUFS, 'driver TX queue stalled')
    GazeboCanBridge.tick(node)
    assert resets == [True] and published[-1] == (0., 0.)


def test_stall_recovery_is_bounded_and_never_bypasses_failed_device_checks(monkeypatch):
    node, _, _, published, _, failing = bridge_fixture(monkeypatch)
    def wrong_device():
        raise ValueError('USB identity/state mismatch')
    node.bus.recover_stalled_transmitter = wrong_device
    node.last_feedback_sent = 8.
    failing[0] = OSError(errno.EAGAIN, 'socket blocked')
    GazeboCanBridge.tick(node)
    assert 'USB identity/state mismatch' in node.fault
    assert not node.recoverable_fault and published[-1] == (0., 0.)
    node.fault = ''; node.last_recovery_attempt = -math.inf; node.transport_recoveries = 3
    node.last_command = 10.; node.last_feedback = 0.
    failing[0] = OSError(errno.ENOBUFS, 'again')
    GazeboCanBridge.tick(node)
    assert 'Repeated CAN transmitter stalls' in node.fault
    assert not node.recoverable_fault


def test_silent_physical_tx_stall_is_detected_without_waiting_for_queue_full(monkeypatch):
    node, _, _, published, feedback, _ = bridge_fixture(monkeypatch)
    def stalled():
        raise OSError(errno.ENOBUFS, 'CAN physical TX completions stalled')
    node.bus.check_transmitter = stalled
    GazeboCanBridge.tick(node)
    assert node.recoverable_fault and 'physical TX' in node.fault
    assert published[-1] == (0., 0.) and not feedback
