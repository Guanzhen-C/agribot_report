import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

sys.path.insert(0, str(Path(__file__).parents[1]/'hil'))
from ros_control import Control


@pytest.mark.parametrize('implement,mask,expected', [
    ('sprayer', 3, True), ('sprayer', 4, False), ('sprayer', 1, False),
    ('shredder', 4, True), ('shredder', 3, False), ('shredder', 0, False),
    ('unknown', 7, False)])
def test_readiness_requires_the_installed_implement_channels(implement, mask, expected):
    values = {'/clock': 5., '/fastlivo_rtk/ready': True,
        '/fastlivo_rtk/odometry': [2., 36., .2],
        '/hil/tracked_can/status': {'implement_kind': implement, 'fault': None,
                                    'truth_fresh': True, 'motors_fresh': True, 'feedback_fresh': True},
        '/hil/isobus/events': {'data': bytes([1, 1, mask, 0x91, 0, 0, 0, 0]).hex()}}
    node = SimpleNamespace(car='Car3', fresh=lambda name, *args: values.get(name))
    assert Control.physical_ready(node) is expected
    values['/hil/tracked_can/status']['motors_fresh'] = False
    assert Control.physical_ready(node) is False


@pytest.mark.parametrize('car', ['Car3', 'Car4'])
def test_other_hil_readiness_requires_fresh_native_feedback(car):
    native = {'fault': '', 'feedback_fresh': True, 'truth_fresh': True,
              'motors_fresh': True, 'implement_kind': 'sprayer'}
    values = {'/clock': 5., '/fastlivo_rtk/ready': True,
        '/fastlivo_rtk/odometry': [2., 36., .2], '/odometry/filtered_navsat': [2., 3., .2],
        '/hil/tracked_can/status': native, '/hil_hitch/can/status': native,
        '/hil_hitch/plant_state': [0.]*9,
        '/hil/isobus/events': {'data': bytes([1, 1, 3, 0x91, 0, 0, 0, 0]).hex()},
        '/hil_hitch/isobus/events': {'data': bytes([0x21, 1, 1, 0, 0, 0, 0, 0]).hex()}}
    node = SimpleNamespace(car=car, fresh=lambda name, *args: values.get(name))
    assert Control.physical_ready(node) is True
    native['feedback_fresh'] = False
    assert Control.physical_ready(node) is False
    native.update(feedback_fresh=True, fault='physical TX stalled')
    assert Control.physical_ready(node) is False


@pytest.mark.parametrize('field', ['feedback_fresh', 'truth_fresh', 'command_fresh'])
def test_ackermann_ready_requires_recent_complete_feedback(field):
    status = {'fault': '', 'feedback_fresh': True, 'truth_fresh': True, 'command_fresh': True}
    values = {'/clock': 5., '/fastlivo_rtk/ready': True,
              '/fastlivo_rtk/odometry': [2., 36., .2], '/hil/can/status': status}
    node = SimpleNamespace(car='Car0', fresh=lambda name, *args: values.get(name))
    assert Control.physical_ready(node) is True
    status[field] = False
    assert Control.physical_ready(node) is False
