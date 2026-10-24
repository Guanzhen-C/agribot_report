"""Validate the complete shipped garage, export assets, scenes and offline inputs."""
import hashlib
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(4 * 1024**2), b''):
            result.update(chunk)
    return result.hexdigest()


def check(root, hashes=False):
    root = Path(root)
    unity = root / 'unity'
    source = root / 'ros2_ws/src'
    issues = []
    counts = dict(vehicles=0, presets=0, builtin_scenes=0, vision_models=0)

    def require(path):
        if not path.is_file():
            issues.append('缺少资源：' + str(path.relative_to(root)))
            return False
        with path.open('rb') as stream:
            if stream.read(80).startswith(b'version https://git-lfs.github.com/spec/v1'):
                issues.append('LFS资源尚未下载：' + str(path.relative_to(root)))
                return False
        return True

    presets = json.loads((unity / 'Assets/AgribotConfig/Resources/farm_vehicle_presets.json').read_text())['presets']
    for preset in presets:
        name = preset['id']
        directory = unity / 'Assets/StreamingAssets/LocalFarmExports' / name
        preview = unity / 'Assets/AgribotConfig/Resources/LocalFarmAssets' / name / 'preview.bytes'
        manifest_path = directory / 'asset_manifest.json'
        if not require(manifest_path) or not require(preview):
            continue
        manifest = json.loads(manifest_path.read_text())
        if manifest.get('vehicleId') != name:
            issues.append('预设清单ID不符：' + name)
        with preview.open('rb') as stream:
            header = stream.read(8)
            if header[:4] != b'AFM1' or len(header) != 8 or struct.unpack('<I', header[4:])[0] != manifest['meshGroups']:
                issues.append('预览网格格式或分组不符：' + name)
        if hashes and digest(preview) != manifest['previewSha256']:
            issues.append('预览校验失败：' + name)
        for record in manifest['files']:
            if Path(record['name']).name != record['name']:
                issues.append('资源清单存在非本地文件名：' + name)
                continue
            path = directory / record['name']
            if require(path) and hashes and digest(path) != record['sha256']:
                issues.append('导出资源校验失败：' + name + '/' + record['name'])
        require(source / 'agribot_vehicle_description/generated' / name / 'models' / (name + '.sdf'))
        counts['presets'] += 1

    vehicles = json.loads((source / 'agribot_farm_sim/config/compatibility.json').read_text())['vehicles']
    for vehicle in vehicles:
        car = vehicle['car']
        directory = unity / 'Assets/AgribotConfig/Resources/LocalGarageThumbnails'
        thumbnail, marker = directory / (car + '.bytes'), directory / (car + '.bytes.source.txt')
        if require(thumbnail) and require(marker):
            original = (unity / 'Assets/Scenes/scene2.unity' if car == 'Car0' else
                        unity / 'Assets/AgribotConfig/Resources/LocalVehicleAppearances/unity_car3/preview.bytes' if car == 'Car3' else
                        unity / 'Assets/AgribotConfig/Resources/LocalFarmAssets' / vehicle['id'] / 'preview.bytes')
            if require(original) and marker.read_text().strip() != 'framing-v2:' + digest(original):
                issues.append('车库缩略图不是当前车型版本：' + car)
            with thumbnail.open('rb') as stream:
                if stream.read(8) != b'\x89PNG\r\n\x1a\n':
                    issues.append('车库缩略图PNG无效：' + car)
        counts['vehicles'] += 1
    require(unity / 'Assets/AgribotConfig/Resources/LocalGarageVehicles/Car0.prefab')
    require(unity / 'Assets/AgribotConfig/Resources/Shaders/CadPreviewTwoSided.shader')
    bundled_font = unity / 'Assets/AgribotConfig/Resources/Fonts/NotoSansCJKsc-Regular.otf'
    if require(bundled_font) and hashes and digest(bundled_font) != digest(unity / 'Assets/Fonts/NotoSansCJKsc-Regular.otf'):
        issues.append('运行时中文字体与原项目字体不一致')

    folders = ('clearpath_farm', 'harbot_greenhouse', 'maize_field', 'aoc_tomato',
               'harvester_strawberry', 'cropcraft_vineyard', 'pic4ser_vegetables', 'wide_headland')
    for folder in folders:
        asset = source / 'agribot_farm_sim/assets' / folder
        if not require(asset / 'world.sdf'):
            continue
        require(asset / 'source.json')
        require(asset / 'map.yaml')
        for uri in ET.parse(asset / 'world.sdf').findall('.//mesh/uri'):
            relative = Path(uri.text)
            if relative.is_absolute() or '..' in relative.parts:
                issues.append('内置场景依赖外部绝对路径：' + folder)
            else:
                require(asset / relative)
        counts['builtin_scenes'] += 1
    if require(source / 'scout_gazebo/worlds/orchard_barriers.world'):
        counts['builtin_scenes'] += 1
    for name in ('yolo26n.pt', 'yolo26n-seg.pt', 'yolo26n-pose.pt'):
        if require(root / 'resources/vision_models' / name):
            counts['vision_models'] += 1
    for name in ('manifest.json', 'hpm6750_ackermann.bin', 'hpm6750_ackermann.elf'):
        require(root / 'resources/deployment/ackermann' / name)
    for path in ('vendor/renderer/include/gsplat_rviz_plugin/splat_cloud.hpp', 'vendor/renderer/LICENSE',
                 'vendor/renderer/src/splat_cloud.cpp', 'vendor/renderer/src/cpu_sorter.cpp',
                 'vendor/renderer/src/perf_local.cpp', 'vendor/shaders/splat.vert', 'vendor/shaders/splat.frag',
                 'plugins/K2GaussianVisual.cpp', 'agribot_k2_sim/lcc_decode.py'):
        require(source / 'agribot_k2_sim' / path)
    if (root / '.local/scanned_scenes/catalog.json').is_file():
        counts['user_imported_scenes_local_only'] = True
    return counts, issues
