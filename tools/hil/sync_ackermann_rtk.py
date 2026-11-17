"""Synchronize the approved Car0 deck mounts into the app-owned HIL bundle."""
import hashlib
import json
import math
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

import yaml

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'ros2_ws/src/agribot_hil_apple'))
from agribot_hil_apple.rtk_visual import ackermann_mounts, rear_deck_antennas


def synchronize():
    mounts = ackermann_mounts()
    app = json.loads((ROOT/'unity/Assets/AgribotConfig/Resources/HilSensors/mounts.json').read_text())
    for key, name in (('master', 'rtk_master'), ('secondary', 'rtk_secondary')):
        pose = next(item['pose'] for item in app['ackermannRtk'] if item['id'] == name)
        if pose['xyz'] != mounts[key] or any(pose['rpy']):
            raise ValueError('Unity and HIL RTK mounting tables must agree before synchronization')
    bundle = ROOT/'resources/hil/scenarios'
    scene = bundle/'ackermann_apple'
    generated = bundle/'shared_assets/6006bf6ce28f'
    source = generated/'sensor_rtk_master_visual.obj'
    mesh = scene/'assets/rtk/sensor_rtk_deck_mount.obj'
    mesh.write_text(rear_deck_antennas(source.read_text(),
        mounts['master'][1]-mounts['secondary'][1], mounts['surfaceOffsetM']))
    provenance = dict(source='@HIL_ROOT@/shared_assets/6006bf6ce28f/'+source.name,
        source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
        result_sha256=hashlib.sha256(mesh.read_bytes()).hexdigest(), mounting=mounts,
        scope='Two original housings and short stems on upper deck ahead of unchanged cargo tray; no rails or tall posts')
    (mesh.parent/'provenance.json').write_text(json.dumps(provenance, indent=2)+'\n')

    def robot_mounts(robot):
        for name, key in (('rtk_master_mount_joint', 'master'), ('rtk_secondary_mount_joint', 'secondary')):
            robot.find(f"joint[@name='{name}']/origin").set('xyz', ' '.join(map(str, mounts[key])))

    tree = ET.parse(scene/'vehicle.urdf')
    robot_mounts(tree.getroot())
    ET.indent(tree, space='  ')
    tree.write(scene/'vehicle.urdf', encoding='utf-8', xml_declaration=True)
    tree = ET.parse(scene/'vehicle.sdf')
    base = tree.find("model/link[@name='base_link']")
    base.find("visual[@name='rtk_master_cad_visual']/pose").text = ' '.join(map(str, [*mounts['master'], 0., 0., 0.]))
    ET.indent(tree, space='  ')
    tree.write(scene/'vehicle.sdf', encoding='utf-8', xml_declaration=True)

    arm_file = scene/'rdk/arm_moveit.yaml'
    arm = yaml.safe_load(arm_file.read_text())
    robot = ET.fromstring(arm['/**']['ros__parameters']['robot_description'])
    robot_mounts(robot)
    arm['/**']['ros__parameters']['robot_description'] = ET.tostring(robot, encoding='unicode')
    arm_file.write_text(yaml.safe_dump(arm, sort_keys=False))
    fusion_file = scene/'rdk/fusion.yaml'
    fusion = yaml.safe_load(fusion_file.read_text())
    fusion['fastlivo_rtk_fusion']['ros__parameters']['base_to_antenna_xyz'] = mounts['master']
    fusion_file.write_text(yaml.safe_dump(fusion, sort_keys=False))
    mission_file = scene/'rdk/mission.json'
    mission = json.loads(mission_file.read_text())
    mission['base_to_antenna'] = mounts['master']
    mission.pop('rtk_antenna_lowering_m', None)
    mission['rtk_mounting'] = mounts
    mission_file.write_text(json.dumps(mission, indent=2)+'\n')
    georef_file = scene/'rdk/georeference.yaml'
    georef = yaml.safe_load(georef_file.read_text())
    spawn, antenna = mission['spawn'], mounts['master']
    c, s = math.cos(spawn[3]), math.sin(spawn[3])
    georef['map_from_enu']['xyz'] = [spawn[0]+c*antenna[0]-s*antenna[1],
        spawn[1]+s*antenna[0]+c*antenna[1], spawn[2]+antenna[2]]
    georef_file.write_text(yaml.safe_dump(georef, sort_keys=False))
    manifest_file = bundle/'bundle.json'
    manifest = json.loads(manifest_file.read_text())
    for relative in manifest['files']:
        if relative.startswith('ackermann_apple/'):
            manifest['files'][relative] = hashlib.sha256((bundle/relative).read_bytes()).hexdigest()
    manifest_file.write_text(json.dumps(manifest, indent=2)+'\n')


if __name__ == '__main__':
    synchronize()
