import runpy
from pathlib import Path
from types import SimpleNamespace as NS


def test_raw_lethal_and_inscribed_cells_remain_distinct():
    source = Path(__file__).resolve().parents[2]/'docs/deliverables/20261116_hil_retest/collision_probe.py'
    cells = runpy.run_path(str(source))['cells']
    meta = NS(size_x=3, size_y=1, resolution=.2,
              origin=NS(position=NS(x=-.3, y=-.1), orientation=NS(x=0., y=0., z=0., w=1.)))
    result = cells([253, 254, 252], meta, [0., 0., 0., 0.])
    assert result['lethal_254_inside'] == 1
    assert result['inscribed_253_inside'] == 1
    assert [row[2] for row in result['cells_map_cost']] == [253., 254.]


def test_translated_rotated_grid_uses_map_coordinates():
    source = Path(__file__).resolve().parents[2]/'docs/deliverables/20261116_hil_retest/collision_probe.py'
    cells = runpy.run_path(str(source))['cells']
    meta = NS(size_x=1, size_y=1, resolution=.1,
              origin=NS(position=NS(x=9.95, y=19.95), orientation=NS(x=0., y=0., z=1., w=0.)))
    result = cells([254], meta, [9.9, 19.9, 0., 0.])
    assert result['lethal_254_inside'] == 1
    assert result['cells_map_cost'] == [[9.9, 19.9, 254.]]
