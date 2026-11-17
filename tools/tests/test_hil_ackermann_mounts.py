import hashlib
import json
import math
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

import numpy as np
import pytest
import yaml

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'ros2_ws/src/agribot_hil_apple'))
from agribot_hil_apple.rtk_visual import ackermann_mounts, RTK_SUPPORTS, RTK_SHORT_STEMS
from agribot_hil_apple.scene import CARGO_PARTS

SCENE = ROOT/'resources/hil/scenarios/ackermann_apple'


def rendered_vertices():
    vertices, used, objects = [], set(), set()
    current = ''
    for line in (SCENE/'assets/rtk/sensor_rtk_deck_mount.obj').read_text().splitlines():
        parts = line.split()
        if not parts:
            continue
        if parts[0] == 'v':
            vertices.append(list(map(float, parts[1:4])))
        elif parts[0] == 'o':
            current = ' '.join(parts[1:])
        elif parts[0] == 'f':
            objects.add(current)
            for value in parts[1:]:
                index = int(value.split('/')[0])
                used.add(index-1 if index > 0 else len(vertices)+index)
    return np.array([vertices[index] for index in sorted(used)]), objects


def test_app_and_hil_share_both_mounts_and_left_master():
    mounts = ackermann_mounts()
    app = json.loads((ROOT/'unity/Assets/AgribotConfig/Resources/HilSensors/mounts.json').read_text())
    assert mounts['leftIsMaster'] is True
    for key, name in [('master', 'rtk_master'), ('secondary', 'rtk_secondary')]:
        assert next(item['pose']['xyz'] for item in app['ackermannRtk'] if item['id'] == name) == mounts[key]
    assert mounts['master'][0] == mounts['secondary'][0] == .12
    assert mounts['master'][1] == -mounts['secondary'][1] == .15


def test_visual_tf_moveit_fusion_and_simulated_fix_have_identical_lever_arms():
    mounts = ackermann_mounts()
    robot = ET.parse(SCENE/'vehicle.urdf')
    arm = yaml.safe_load((SCENE/'rdk/arm_moveit.yaml').read_text())
    moveit = ET.fromstring(arm['/**']['ros__parameters']['robot_description'])
    for name, key in [('rtk_master_mount_joint', 'master'), ('rtk_secondary_mount_joint', 'secondary')]:
        for model in (robot, moveit):
            assert list(map(float, model.find(f"joint[@name='{name}']/origin").get('xyz').split())) == mounts[key]
    visual = ET.parse(SCENE/'vehicle.sdf').findtext("model/link/visual[@name='rtk_master_cad_visual']/pose")
    assert list(map(float, visual.split())) == [*mounts['master'], 0., 0., 0.]
    fusion = yaml.safe_load((SCENE/'rdk/fusion.yaml').read_text())
    assert fusion['fastlivo_rtk_fusion']['ros__parameters']['base_to_antenna_xyz'] == mounts['master']
    mission = json.loads((SCENE/'rdk/mission.json').read_text())
    assert mission['base_to_antenna'] == mounts['master']
    spawn, antenna = mission['spawn'], mounts['master']
    c, s = math.cos(spawn[3]), math.sin(spawn[3])
    expected = [spawn[0]+c*antenna[0]-s*antenna[1], spawn[1]+s*antenna[0]+c*antenna[1], spawn[2]+antenna[2]]
    assert yaml.safe_load((SCENE/'rdk/georeference.yaml').read_text())['map_from_enu']['xyz'] == pytest.approx(expected)


def test_short_stems_rest_on_deck_and_clear_the_unchanged_fruit_tray():
    mounts = ackermann_mounts()
    points, objects = rendered_vertices()
    assert objects.isdisjoint(RTK_SUPPORTS-RTK_SHORT_STEMS)
    assert objects.issuperset(RTK_SHORT_STEMS)
    assert points[:, 2].min()+mounts['master'][2] == pytest.approx(mounts['supportSurfaceZ'])
    assert points[:, 2].max()-points[:, 2].min() == pytest.approx(.1228, abs=.0001)
    world = points+mounts['master']
    # All antenna geometry is ahead of the tray's foremost wall, not inside it.
    cargo_front = max(xyz[0]+size[0]/2 for _, xyz, size in CARGO_PARTS)
    assert world[:, 0].min()-cargo_front > .01
    for y in (.15, -.15):
        head = world[np.abs(world[:, 1]-y) < .05]
        assert len(head) > 100
        assert (head[:, 1].min()+head[:, 1].max())/2 == pytest.approx(y, abs=.00001)


def test_tray_geometry_is_identical_in_sdf_urdf_and_moveit():
    sdf = ET.parse(SCENE/'vehicle.sdf').find("model/link[@name='base_link']")
    urdf = ET.parse(SCENE/'vehicle.urdf').find("link[@name='base_link']")
    arm = yaml.safe_load((SCENE/'rdk/arm_moveit.yaml').read_text())
    moveit = ET.fromstring(arm['/**']['ros__parameters']['robot_description']).find("link[@name='base_link']")
    assert next(size for name, _, size in CARGO_PARTS if name == 'cargo_floor') == [.18, .28, .01]
    for name, xyz, size in CARGO_PARTS:
        for kind in ('visual', 'collision'):
            item = sdf.find(f"{kind}[@name='{name}_{kind}']")
            assert list(map(float, item.findtext('pose').split()))[:3] == xyz
            assert list(map(float, item.findtext('geometry/box/size').split())) == size
        for model in (urdf, moveit):
            item = model.find(f"collision[@name='{name}']")
            assert list(map(float, item.find('origin').get('xyz').split())) == xyz
            assert list(map(float, item.find('geometry/box').get('size').split())) == size


def test_bundle_manifest_and_mesh_provenance_match_changed_assets():
    bundle = SCENE.parent
    manifest = json.loads((bundle/'bundle.json').read_text())
    for name, expected in manifest['files'].items():
        if name.startswith('ackermann_apple/'):
            assert hashlib.sha256((bundle/name).read_bytes()).hexdigest() == expected, name
    provenance = json.loads((SCENE/'assets/rtk/provenance.json').read_text())
    assert provenance['mounting'] == ackermann_mounts()
    assert provenance['result_sha256'] == hashlib.sha256((SCENE/'assets/rtk/sensor_rtk_deck_mount.obj').read_bytes()).hexdigest()
