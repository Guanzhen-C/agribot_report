import ast
import importlib.util
import math
from pathlib import Path
from types import SimpleNamespace

import numpy as np
from matplotlib.path import Path as PolygonPath
import pytest

ROOT = Path(__file__).parents[2]
HERE = ROOT/'docs/deliverables/20261116_hil_retest'
spec = importlib.util.spec_from_file_location('hil_pose_distance', HERE/'pose_audit.py')
distance = importlib.util.module_from_spec(spec)
spec.loader.exec_module(distance)
source = HERE/'navigation_obstacle_checks.py'
tree = ast.parse(source.read_text())
scope = dict(np=np, PolygonPath=PolygonPath, distances_to_path=distance.distances_to_path)
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n, ast.FunctionDef)
                            and n.name == 'polygon_clearance'], type_ignores=[]), str(source), 'exec'), scope)
clearance = scope['polygon_clearance']


@pytest.mark.parametrize('offset,expected', [(2., 1.), (1., 0.), (.5, 0.), (0., 0.)])
def test_complete_footprint_distance_detects_intersection_and_tangency(offset, expected):
    body = np.array([[-.5, -.5], [.5, -.5], [.5, .5], [-.5, .5]])
    assert clearance(body, body+[offset, 0.]) == pytest.approx(expected)


def test_containment_is_also_collision():
    outer = [[-2., -2.], [2., -2.], [2., 2.], [-2., 2.]]
    inner = [[-.1, -.1], [.1, -.1], [.1, .1], [-.1, .1]]
    assert clearance(outer, inner) == 0.


def box_grid():
    return SimpleNamespace(info=SimpleNamespace(resolution=.1, width=40, height=40,
        origin=SimpleNamespace(position=SimpleNamespace(x=0., y=0.))), data=[0]*1600)


def marked(message):
    namespace = dict(math=math)
    exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n, ast.FunctionDef)
        and n.name == 'marked_box_cells'], type_ignores=[]), str(source), 'exec'), namespace)
    return namespace['marked_box_cells'](message, [2., 2.])


def test_surface_mark_is_detected_without_marked_box_center():
    grid = box_grid()
    grid.data[20*40+22] = 100
    assert grid.data[20*40+20] == 0
    assert len(marked(grid)) == 1


def test_unrelated_obstacle_or_inflation_is_not_a_new_box_detection():
    grid = box_grid()
    grid.data[20*40+28] = 100
    grid.data[20*40+22] = 99
    assert marked(grid) == []
