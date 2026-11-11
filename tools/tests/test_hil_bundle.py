import importlib.util
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

import pytest
import yaml

sys.path.insert(0, str(Path(__file__).parents[1]/'hil'))
from bundle import Importer, materialize


def test_relocation_preserves_nested_urdf_and_original_assets(tmp_path):
    assets = tmp_path/'original_models'
    assets.mkdir()
    (assets/'blade.obj').write_text('mtllib blade.mtl\nv 0 0 0\n')
    (assets/'blade.mtl').write_text('newmtl steel\nmap_Kd steel.png\n')
    (assets/'steel.png').write_bytes(b'texture fixture')
    seed = tmp_path/'seeds/tractor_harrow'
    seed.mkdir(parents=True)
    (seed/'world.sdf').write_text('<sdf><world><uri>'+str(assets/'blade.obj')+'</uri></world></sdf>')
    robot = '<robot name="arm"><link name="tool"><visual><mesh filename="'+str(assets/'blade.obj')+'"/></visual></link></robot>'
    (seed/'moveit.yaml').write_text(yaml.safe_dump({'robot_description': robot}))
    bundle = tmp_path/'bundle'
    Importer(tmp_path/'seeds', bundle).run()
    destination = materialize(bundle, tmp_path/'runtime')
    assert not (destination/'tractor_harrow/world.sdf').read_text().startswith('<?xml')
    path = Path(ET.parse(destination/'tractor_harrow/world.sdf').findtext('world/uri'))
    assert path.is_file() and path.read_text() == (assets/'blade.obj').read_text()
    assert (path.parent/'blade.mtl').is_file() and (path.parent/'steel.png').is_file()
    value = yaml.safe_load((destination/'tractor_harrow/moveit.yaml').read_text())
    robot = ET.fromstring(value['robot_description'])
    assert Path(robot.find('link/visual/mesh').get('filename')) == path
    assert str(assets) not in (bundle/'tractor_harrow/moveit.yaml').read_text()


def test_corrupt_bundle_is_rejected_before_destination_is_created(tmp_path):
    seeds = tmp_path/'seeds'
    seeds.mkdir()
    (seeds/'mission.json').write_text('{}')
    bundle = tmp_path/'bundle'
    Importer(seeds, bundle).run()
    (bundle/'mission.json').write_text('{"modified":true}')
    with pytest.raises(ValueError):
        materialize(bundle, tmp_path/'runtime')
    assert not (tmp_path/'runtime').exists()
