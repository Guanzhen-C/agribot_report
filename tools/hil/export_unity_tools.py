"""Run with Blender to export the exact verified SDF tool poses to AFM previews."""
import hashlib
import json
from pathlib import Path
import struct
import sys
import xml.etree.ElementTree as ET

import bpy
from mathutils import Euler, Matrix, Vector


ROOT = Path(sys.argv[sys.argv.index('--')+1]).resolve()
PIXELS = {}


def pose(text):
    values = list(map(float, (text or '0 0 0 0 0 0').split()))
    return Matrix.Translation(Vector(values[:3])) @ Euler(values[3:], 'XYZ').to_matrix().to_4x4()


def sampled_color(obj, triangle):
    material = obj.data.materials[triangle.material_index] if obj.data.materials else None
    color = tuple(material.diffuse_color) if material else (.2, .25, .22, 1)
    if material and material.use_nodes and obj.data.uv_layers.active:
        images = [node.image for node in material.node_tree.nodes if node.type == 'TEX_IMAGE' and node.image]
        if images:
            image = images[0]
            if image.name not in PIXELS:
                PIXELS[image.name] = tuple(image.pixels)
            uv = sum((obj.data.uv_layers.active.data[i].uv for i in triangle.loops), Vector((0, 0)))/3
            width, height = image.size
            offset = ((min(height-1, int((uv.y % 1)*height))*width)+min(width-1, int((uv.x % 1)*width)))*4
            color = PIXELS[image.name][offset:offset+4]
    return tuple(round(float(c)*32)/32 for c in color[:3])+(1.,)


def export(identifier, runtime):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    PIXELS.clear()
    bundle = ROOT/'resources/hil/scenarios'
    sdf = bundle/runtime/'vehicle.sdf'
    model = ET.parse(sdf).getroot().find('model')
    groups, objects = {}, []
    for link in model.findall('link'):
        if not link.get('name', '').startswith('work_'): continue
        for visual in link.findall('visual'):
            mesh = visual.find('geometry/mesh')
            if mesh is None: continue
            path = mesh.findtext('uri').removeprefix('file://').replace('@HIL_ROOT@', str(bundle))
            previous = set(bpy.data.objects)
            bpy.ops.wm.collada_import(filepath=path)
            transform = pose(link.findtext('pose')) @ pose(visual.findtext('pose'))
            scale = list(map(float, mesh.findtext('scale', '1 1 1').split()))
            transform = transform @ Matrix.Diagonal(Vector(scale+[1.]))
            for obj in set(bpy.data.objects)-previous:
                if obj.type != 'MESH': continue
                obj.matrix_world = transform @ obj.matrix_world
                objects.append(obj)
                obj.data.calc_loop_triangles()
                for triangle in obj.data.loop_triangles:
                    color = sampled_color(obj, triangle)
                    vertices = [obj.matrix_world @ obj.data.vertices[i].co for i in triangle.vertices]
                    groups.setdefault(color, []).append(tuple(c for v in vertices for c in v))
    if not objects: raise RuntimeError('No original CAD tool meshes found')
    resource = ROOT/'unity/Assets/AgribotConfig/Resources/HilImplements'/identifier
    resource.mkdir(parents=True, exist_ok=True)
    chunks = [(color, faces[start:start+250000]) for color, faces in groups.items()
              for start in range(0, len(faces), 250000)]
    with (resource/'preview.bytes').open('wb') as output:
        output.write(b'AFM1'+struct.pack('<I', len(chunks)))
        for index, (color, faces) in enumerate(chunks):
            name = ('work_'+str(index)).encode()
            output.write(struct.pack('<I', len(name))+name+struct.pack('<4fI', *color, len(faces)))
            for face in faces: output.write(struct.pack('<9f', *face))
    manifest = {'schemaVersion': 1, 'vehicleId': identifier, 'meshGroups': len(chunks),
                'triangles': sum(len(faces) for _, faces in chunks),
                'previewSha256': hashlib.sha256((resource/'preview.bytes').read_bytes()).hexdigest(),
                'sourceSdfSha256': hashlib.sha256(sdf.read_bytes()).hexdigest()}
    (resource/'manifest.json').write_text(json.dumps(manifest, indent=2)+'\n')
    bounds = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    center = (Vector(tuple(min(v[i] for v in bounds) for i in range(3)))+
              Vector(tuple(max(v[i] for v in bounds) for i in range(3))))/2
    extent = max(max(v[i] for v in bounds)-min(v[i] for v in bounds) for i in range(3))
    bpy.ops.object.camera_add(location=center+Vector((1.6, -2., 1.3))*extent)
    camera = bpy.context.object
    camera.rotation_euler = (center-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type, camera.data.ortho_scale = 'ORTHO', extent*1.6
    scene = bpy.context.scene
    scene.camera = camera
    for offset, power in (((2, -2, 3), 450), ((-2, -1, 2), 300), ((0, 3, 3), 350)):
        bpy.ops.object.light_add(type='AREA', location=center+Vector(offset)*extent)
        light = bpy.context.object
        light.data.energy, light.data.size = power, extent*2
        light.rotation_euler = (center-light.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new('HilThumbnailWorld')
    scene.world.color = (.25, .25, .25)
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(resource/'thumbnail.png')
    bpy.ops.render.render(write_still=True)
    thumbnail = ROOT/'unity/Assets/AgribotConfig/Resources/LocalGarageThumbnails'/(identifier+'.bytes')
    thumbnail.write_bytes((resource/'thumbnail.png').read_bytes())
    (resource/'thumbnail.png').unlink()
    print(json.dumps(manifest))


for identifier, runtime in (('hil_sprayer', 'tracked_sprayer'), ('hil_shredder', 'tracked_shredder')):
    export(identifier, runtime)
