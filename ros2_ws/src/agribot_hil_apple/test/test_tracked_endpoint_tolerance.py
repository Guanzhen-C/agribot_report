import time
from types import SimpleNamespace

import pytest

from agribot_hil_apple.tracked_orchard_mission import OrchardMission


@pytest.mark.parametrize('error,passed', [(0.4185015549, True), (.45, False), (float('nan'), False)])
def test_actual_endpoint_uses_only_half_wheelbase_without_extra_margin(error, passed):
    node = SimpleNamespace(
        wait_ready=lambda _: None, gateway_ready_at=time.monotonic(),
        bridge={'motion_authorized': True}, navigate_route=lambda: None, stop=lambda: None,
        manifest={'finish': [0., 0., 0.], 'goal_tolerance_m': .4185015549}, truth=[error, 0., 0.],
        report={'actual_work': [[0., 100., 100.]]}, route_passage_events=[], persist=lambda: None)
    if passed:
        OrchardMission.run(node)
        assert node.report['status'] == 'succeeded'
    else:
        with pytest.raises(RuntimeError, match='half-wheelbase tolerance'):
            OrchardMission.run(node)
