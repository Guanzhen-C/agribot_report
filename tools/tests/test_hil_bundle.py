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


def test_refresh_replaces_changed_cached_meshes_and_keeps_unchanged_media(tmp_path):
    seeds = tmp_path/'seeds'
    seeds.mkdir()
    (seeds/'antenna.obj').write_text('v 0 0 1\n')
    (seeds/'texture.png').write_bytes(b'unchanged texture')
    bundle = tmp_path/'bundle'
    Importer(seeds, bundle).run()
    runtime = materialize(bundle, tmp_path/'runtime')
    texture_stat = (runtime/'texture.png').stat()
    (seeds/'antenna.obj').write_text('v 0 0 2\n')
    next_bundle = tmp_path/'updated_bundle'
    Importer(seeds, next_bundle).run()
    materialize(next_bundle, runtime, refresh=True)
    assert (runtime/'antenna.obj').read_bytes() == (seeds/'antenna.obj').read_bytes()
    assert (runtime/'texture.png').stat().st_mtime_ns == texture_stat.st_mtime_ns


def test_refresh_rejects_corrupt_source_without_touching_cached_media(tmp_path):
    seeds = tmp_path/'seeds'
    seeds.mkdir()
    (seeds/'antenna.obj').write_text('v 0 0 1\n')
    bundle = tmp_path/'bundle'
    Importer(seeds, bundle).run()
    runtime = materialize(bundle, tmp_path/'runtime')
    before = (runtime/'antenna.obj').read_bytes()
    (bundle/'antenna.obj').write_text('corrupt source')
    with pytest.raises(ValueError, match='integrity mismatch'):
        materialize(bundle, runtime, refresh=True)
    assert (runtime/'antenna.obj').read_bytes() == before
