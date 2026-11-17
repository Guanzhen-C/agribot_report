"""Export the existing HIL sensor CAD, including one short-stem RTK antenna."""
import hashlib
import json
from pathlib import Path
import struct
import sys
import xml.etree.ElementTree as ET

import bpy
from mathutils import Vector

ROOT = Path(sys.argv[sys.argv.index('--')+1]).resolve()
BUNDLE = ROOT/'resources/hil/scenarios'
OUTPUT = ROOT/'unity/Assets/AgribotConfig/Resources/HilSensors'


def export(identifier, visual):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source = Path(visual.findtext('geometry/mesh/uri').removeprefix('file://').replace('@HIL_ROOT@', str(BUNDLE)))
    bpy.ops.import_scene.obj(filepath=str(source), axis_forward='Y', axis_up='Z', use_split_objects=True)
    groups = {}
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH': continue
        obj.data.calc_loop_triangles()
        for triangle in obj.data.loop_triangles:
            points = [obj.matrix_world @ obj.data.vertices[index].co for index in triangle.vertices]
            if identifier == 'rtk_antenna' and sum(point.y for point in points)/3 < -.29:
                continue
            material = obj.data.materials[triangle.material_index] if obj.data.materials else None
            color = tuple(material.diffuse_color) if material else (.2, .2, .2, 1.)
            groups.setdefault(color, []).append(tuple(float(value) for point in points for value in point))
    if not groups: raise ValueError('No sensor CAD triangles: '+identifier)
    directory = OUTPUT/identifier
    directory.mkdir(parents=True, exist_ok=True)
    with (directory/'preview.bytes').open('wb') as stream:
        stream.write(b'AFM1'+struct.pack('<I', len(groups)))
        for index, (color, faces) in enumerate(groups.items()):
            name = ('sensor_'+str(index)).encode()
            stream.write(struct.pack('<I', len(name))+name+struct.pack('<4fI', *color, len(faces)))
            for face in faces: stream.write(struct.pack('<9f', *face))
    coordinates = [face[offset:offset+3] for faces in groups.values() for face in faces for offset in (0, 3, 6)]
    minimum = [min(point[axis] for point in coordinates) for axis in range(3)]
    maximum = [max(point[axis] for point in coordinates) for axis in range(3)]
    manifest = {'schemaVersion': 1, 'vehicleId': identifier, 'meshGroups': len(groups),
        'triangles': sum(len(faces) for faces in groups.values()), 'minimumRos': minimum, 'maximumRos': maximum,
        'previewSha256': hashlib.sha256((directory/'preview.bytes').read_bytes()).hexdigest(),
        'sourceMeshSha256': hashlib.sha256(source.read_bytes()).hexdigest()}
    (directory/'manifest.json').write_text(json.dumps(manifest, indent=2)+'\n')
    print(json.dumps(manifest), flush=True)
    return manifest


model = ET.parse(BUNDLE/'tracked_sprayer/vehicle.sdf').getroot().find('model')
for identifier, visual_name in (('imu', 'imu_cad_visual'), ('lidar', 'lidar_cad_visual'),
                               ('right_camera', 'right_camera_cad_visual'), ('rtk_antenna', 'rtk_master_cad_visual')):
    visual = model.find(".//visual[@name='"+visual_name+"']")
    manifest = export(identifier, visual)
    if identifier == 'rtk_antenna': stem_offset = -manifest['minimumRos'][2]
mounts = {'schemaVersion': 1, 'rtkSurfaceOffsetM': stem_offset, 'tracked': [], 'ackermannRtk': []}
for runtime, key in (('tracked_sprayer', 'tracked'), ('ackermann_apple', 'ackermannRtk')):
    robot = ET.parse(BUNDLE/runtime/'vehicle.urdf').getroot()
    frames = {'imu_link': 'imu', 'lidar_link': 'lidar', 'right_camera_link': 'right_camera',
              'rtk_link': 'rtk_master', 'rtk_secondary_link': 'rtk_secondary'}
    for joint in robot.findall('joint'):
        frame = joint.find('child').get('link')
        if frame not in frames or (key == 'ackermannRtk' and not frames[frame].startswith('rtk_')): continue
        origin = joint.find('origin')
        mounts[key].append({'id': frames[frame], 'frameId': frame, 'pose': {
            'xyz': list(map(float, origin.get('xyz').split())),
            'rpy': list(map(float, origin.get('rpy', '0 0 0').split()))}})
mount_file = OUTPUT/'mounts.json'
if mount_file.is_file():
    previous = json.loads(mount_file.read_text())
    # Keep the approved app layout; sync_ackermann_rtk.py verifies HIL agrees.
    if previous.get('ackermannRtkMounting'):
        mounts['ackermannRtk'] = previous['ackermannRtk']
        mounts['ackermannRtkMounting'] = previous['ackermannRtkMounting']
mount_file.write_text(json.dumps(mounts, indent=2)+'\n')
