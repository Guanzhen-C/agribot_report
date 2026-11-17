"""Replace only RTK records in existing canonical farm assets, without body LOD changes."""
import copy
import hashlib
import json
from pathlib import Path
import shutil
import struct
import sys
import xml.etree.ElementTree as ET

import numpy as np
import yaml

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'ros2_ws/src/agribot_farm_sim/scripts'))
from prepare_full_detail_preview import digest, records, save


def body_digest(parts):
    checksum = hashlib.sha256()
    for name, header, data in parts:
        if name != b'rtk_link': checksum.update(name+header+data)
    return checksum.hexdigest()


def sync_car_thumbnails(identifier, preview_sha256):
    track_marker = ROOT/'unity/Assets/AgribotConfig/Resources/LocalFarmAssets'/(identifier+'_icon.png.source.txt')
    if track_marker.is_file():
        track_marker.write_text(preview_sha256+'\n')
    catalog = json.loads((ROOT/'unity/Assets/AgribotConfig/Resources/farm_vehicle_presets.json').read_text())
    preset = next(item for item in catalog['presets'] if item['id'] == identifier)
    car = preset.get('garageName')
    if not car:
        return
    directory = ROOT/'unity/Assets/AgribotConfig/Resources/LocalGarageThumbnails'
    # Both aliases must remain current or the editor replaces one with a legacy icon.
    for alias in (car, identifier):
        (directory/(alias+'.bytes.source.txt')).write_text('framing-v2:'+preview_sha256+'\n')
    shutil.copyfile(directory/(car+'.bytes'), directory/(identifier+'.bytes'))


def write_obj(path, pieces):
    material = path.with_suffix('.mtl')
    offset = 1
    with path.open('w') as stream, material.open('w') as palette:
        stream.write('# Existing HIL antenna CAD with short stems; no support rack\nmtllib '+material.name+'\n')
        for index, (_, header, data) in enumerate(pieces):
            rgba = struct.unpack('<4fI', header)[:4]
            points = np.frombuffer(data, dtype='<f4').reshape(-1, 3)
            palette.write('newmtl antenna_'+str(index)+'\nKd '+' '.join(map(str, rgba[:3]))+'\nKa 0.05 0.05 0.05\n\n')
            stream.write('o antenna_'+str(index)+'\nusemtl antenna_'+str(index)+'\n')
            np.savetxt(stream, points, fmt='v %.9g %.9g %.9g')
            faces = np.arange(offset, offset+len(points)).reshape(-1, 3)
            np.savetxt(stream, faces, fmt='f %d %d %d')
            offset += len(points)


def update_xml(path, positions):
    ET.register_namespace('xacro', 'http://www.ros.org/wiki/xacro')
    tree = ET.parse(path, parser=ET.XMLParser(target=ET.TreeBuilder(insert_comments=True)))
    for joint in tree.findall('.//joint'):
        child = joint.find('child')
        if child is not None and child.get('link') in positions:
            joint.find('origin').set('xyz', ' '.join(map(str, positions[child.get('link')])))
    for link in tree.findall('.//link'):
        if link.get('name') in positions and link.find('pose') is not None:
            values = link.findtext('pose').split()
            link.find('pose').text = ' '.join(map(str, positions[link.get('name')]))+' '+' '.join(values[3:])
    tree.write(path, encoding='utf-8', xml_declaration=True)


def update_yaml(value, master, secondary):
    if isinstance(value, dict):
        for key, item in list(value.items()):
            if key in ('base_to_antenna_xyz', 'base_to_master_antenna_m'): value[key] = master
            elif key in ('rtk_master', 'rtk_secondary') and isinstance(item, dict) and 'xyz' in item:
                item['xyz'] = master if key == 'rtk_master' else secondary
            else: update_yaml(item, master, secondary)
    elif isinstance(value, list):
        for item in value: update_yaml(item, master, secondary)


def update(identifier, antenna, surface_offset):
    raw = ROOT/'unity/Assets/StreamingAssets/LocalFarmExports'/identifier
    config_path = raw/'vehicle_config.json'
    config = json.loads(config_path.read_text())
    if config['metadata'].get('simulationRtkMount', {}).get('revision') == 1:
        preview = ROOT/'unity/Assets/AgribotConfig/Resources/LocalFarmAssets'/identifier/'preview.bytes'
        sync_car_thumbnails(identifier, digest(preview))
        return {'id': identifier, 'already_updated': True}
    before_config = copy.deepcopy(config)
    sensors = [sensor for sensor in config['sensors'] if sensor['id'].startswith('rtk_')]
    old_positions = {sensor['id']: sensor['pose']['xyz'][:] for sensor in sensors}
    for sensor in sensors: sensor['pose']['xyz'][2] += surface_offset-.5202005
    config['metadata']['simulationRtkMount'] = {'revision': 1, 'style': 'hil_short_stems_no_rack',
        'originalAntennaPoses': old_positions, 'supportSurfacePreserved': True,
        'antennaPreviewSha256': digest(ROOT/'unity/Assets/AgribotConfig/Resources/HilSensors/rtk_antenna/preview.bytes')}
    pieces = []
    local = []
    master = sensors[0]['pose']['xyz']
    for sensor in sensors:
        shift = np.asarray(sensor['pose']['xyz'])
        for _, header, data in antenna:
            points = np.frombuffer(data, dtype='<f4').reshape(-1, 3)
            pieces.append((b'rtk_link', header, (points+shift).astype('<f4').tobytes()))
            local.append((b'rtk_link', header, (points+shift-master).astype('<f4').tobytes()))
    preview = ROOT/'unity/Assets/AgribotConfig/Resources/LocalFarmAssets'/identifier/'preview.bytes'
    prior = records(preview)
    before_body = body_digest(prior)
    updated = []
    inserted = False
    for part in prior:
        if part[0] == b'rtk_link':
            if not inserted: updated.extend(pieces); inserted = True
        else: updated.append(part)
    if not inserted: raise ValueError('Missing original RTK preview: '+identifier)
    if body_digest(updated) != before_body: raise ValueError('Non-RTK CAD changed')
    stats = save(preview, updated)
    config_path.write_text(json.dumps(config, ensure_ascii=False, indent=2)+'\n')
    write_obj(raw/'sensor_rtk_master_visual.obj', local)
    manifest_path = raw/'asset_manifest.json'
    manifest = json.loads(manifest_path.read_text())
    manifest.update(stats)
    for file in manifest['files']:
        if file['name'] in ('vehicle_config.json', 'sensor_rtk_master_visual.obj', 'sensor_rtk_master_visual.mtl'):
            file['sha256'] = digest(raw/file['name'])
    manifest['sourceConfigSha256'] = digest(config_path)
    if 'previewGeometry' in manifest: manifest['previewGeometry']['canonicalConfigSha256'] = digest(config_path)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n')
    generated = ROOT/'ros2_ws/src/agribot_vehicle_description/generated'/identifier
    if json.loads((generated/'vehicle_config.json').read_text()) != before_config:
        raise ValueError('Canonical source mismatch: '+identifier)
    shutil.copyfile(config_path, generated/'vehicle_config.json')
    for file in ('sensor_rtk_master_visual.obj', 'sensor_rtk_master_visual.mtl'):
        shutil.copyfile(raw/file, generated/'models'/file)
    positions = {sensor['frameId']: sensor['pose']['xyz'] for sensor in sensors}
    for path in list((generated/'urdf').glob('*.xacro'))+list((generated/'models').glob('*.sdf')):
        update_xml(path, positions)
    for path in (generated/'config').rglob('*.yaml'):
        if path.name not in ('sensor_mounts.yaml', 'fastlivo_rtk_fusion.yaml', 'rtk_map_initializer.yaml'): continue
        value = yaml.safe_load(path.read_text())
        update_yaml(value, sensors[0]['pose']['xyz'], sensors[1]['pose']['xyz'])
        path.write_text(yaml.safe_dump(value, sort_keys=False, allow_unicode=True))
    source_manifest = generated/'manifest.json'
    source = json.loads(source_manifest.read_text())
    for file in source['files']:
        path = generated/file['path']
        file.update(sizeBytes=path.stat().st_size, sha256=digest(path))
    source_manifest.write_text(json.dumps(source, ensure_ascii=False, indent=2)+'\n')
    # Garage thumbnails hide all sensor groups; unchanged body triangles preserve their pixels.
    sync_car_thumbnails(identifier, stats['previewSha256'])
    return {'id': identifier, 'non_rtk_geometry_unchanged': True, 'body_preview_sha256': before_body,
            'original_positions': old_positions, 'new_positions': {sensor['id']: sensor['pose']['xyz'] for sensor in sensors}}


if __name__ == '__main__':
    sensor_root = ROOT/'unity/Assets/AgribotConfig/Resources/HilSensors'
    antenna = records(sensor_root/'rtk_antenna/preview.bytes')
    surface_offset = json.loads((sensor_root/'mounts.json').read_text())['rtkSurfaceOffsetM']
    catalog = json.loads((ROOT/'unity/Assets/AgribotConfig/Resources/farm_vehicle_presets.json').read_text())
    report = []
    for preset in catalog['presets']:
        result = update(preset['id'], antenna, surface_offset)
        report.append(result)
        print(json.dumps(result), flush=True)
    output = ROOT/'.validation/hil_sensor_alignment/farm-rtk-update.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2)+'\n')
