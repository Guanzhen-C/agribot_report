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
                                    'truth_fresh': True, 'motors_fresh': True},
        '/hil/isobus/events': {'data': bytes([1, 1, mask, 0x91, 0, 0, 0, 0]).hex()}}
    node = SimpleNamespace(car='Car3', fresh=lambda name, *args: values.get(name))
    assert Control.physical_ready(node) is expected
    values['/hil/tracked_can/status']['motors_fresh'] = False
    assert Control.physical_ready(node) is False
