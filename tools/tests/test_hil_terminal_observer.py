from pathlib import Path
import runpy

import pytest


def evaluate(car='Car3', position=(.4, 0), velocity=(0., 0., 0.), span=5., new=(), sparse=False):
    source = Path(__file__).resolve().parents[2]/'docs/deliverables/20261116_hil_retest/terminal_hold.py'
    check = runpy.run_path(str(source))['evaluate']
    ticks = [0., 2.5, 5.] if sparse else [i/20 for i in range(101)]
    samples = [dict(clock=t, xy=position, linear_velocity=velocity) for t in ticks]
    return check(car, (0., 0.), samples, span, {'/compute_path_through_poses': {'old'}}, new, [])


def test_terminal_observer_uses_actual_half_wheelbase_without_extra_margin():
    assert evaluate()['passed']
    assert not evaluate(position=(.42, 0))['passed']
    assert not evaluate(car='Car0', position=(.27, 0))['passed']
    assert evaluate(car='Car4', position=(1., 0))['passed']


@pytest.mark.parametrize('arguments', [dict(velocity=(.021, 0, 0)), dict(span=4.99),
                                     dict(new={('planner', 'new')}), dict(sparse=True)])
def test_motion_replanning_or_insufficient_observation_cannot_pass(arguments):
    assert not evaluate(**arguments)['passed']
