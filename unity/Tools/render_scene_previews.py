"""Render the actual ROS world meshes for the scenario browser (run in Blender)."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
from bpy_extras.node_shader_utils import ShaderImageTextureWrapper
from mathutils import Vector


probe = bpy.data.images.new('ColorSpaceProbe', 1, 1)
spaces = probe.colorspace_settings.bl_rna.properties['name'].enum_items.keys()
if 'Non-Color' not in spaces:
    # Debian's fallback config names raw linear texture data "Linear".
    original_init = ShaderImageTextureWrapper.__init__
    def raw_texture_init(self, *args, **kwargs):
        if kwargs.get('colorspace_name') == 'Non-Color':
            kwargs['colorspace_name'] = 'Linear'
        original_init(self, *args, **kwargs)
    ShaderImageTextureWrapper.__init__ = raw_texture_init
bpy.data.images.remove(probe)


def render(mesh, output):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    if mesh.suffix == '.obj':
        bpy.ops.import_scene.obj(filepath=str(mesh), axis_forward='Y', axis_up='Z')
    else:
        bpy.ops.wm.collada_import(filepath=str(mesh))
        if mesh.name == 'orchard_world.dae':
            for name in ('orchard_trunks.dae', 'orchard_leaves.dae'):
                bpy.ops.wm.collada_import(filepath=str(mesh.parent / name))
    objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    points = [obj.matrix_world @ Vector(point) for obj in objects for point in obj.bound_box]
    if not points:
        raise ValueError('Empty world mesh: ' + str(mesh))
    low = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    center = (low + high) / 2
    span = max(high.x - low.x, high.y - low.y)
    for mat in bpy.data.materials:
        mat.blend_method = 'CLIP'
        mat.alpha_threshold = .35
        mat.use_backface_culling = False
    bpy.ops.object.camera_add(location=center + Vector((-span * .60, -span * .75, span * .65)))
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    bpy.context.view_layer.update()
    camera_points = [camera.matrix_world.inverted() @ point for point in points]
    camera.data.ortho_scale = max(max(abs(p.x) for p in camera_points) * 2,
                                 max(abs(p.y) for p in camera_points) * 2 * 960 / 540) * 1.04
    camera.data.clip_end = max(1000, span * 10)
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type='SUN', location=center + Vector((0, 0, span)))
    bpy.context.object.rotation_euler = (.5, -.4, -.3)
    bpy.context.object.data.energy = 2
    world = bpy.context.scene.world
    if world is None:
        world = bpy.data.worlds.new('PreviewWorld')
        bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (.24, .30, .35, 1)
    world.node_tree.nodes['Background'].inputs[1].default_value = .7
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = max(.2, span / 50)
    scene.eevee.taa_render_samples = 32
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGB'
    scene.render.use_file_extension = False
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    output.with_suffix(output.suffix + '.rendered').write_text(json.dumps(
        dict(source=str(mesh), sha256=hashlib.sha256(mesh.read_bytes()).hexdigest())))


parser = argparse.ArgumentParser()
parser.add_argument('--workspace-src', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
scenes = json.loads((args.workspace_src / 'agribot_farm_sim/config/scenarios.json').read_text())['scenarios']
args.output.mkdir(parents=True, exist_ok=True)
for item in scenes:
    mesh = args.workspace_src / 'agribot_farm_sim/assets' / item['source'] / 'scene.obj'
    if item['id'] == 'orchard':
        mesh = args.workspace_src / 'scout_gazebo/meshes/orchard_world.dae'
    render(mesh, args.output / ('Scenario_' + item['id'] + '.bytes'))
    print('SCENARIO_PREVIEW_COMPLETE=' + item['id'], flush=True)
