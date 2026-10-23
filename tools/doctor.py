#!/usr/bin/env python3
"""Check delivery completeness without starting a simulation or vehicle."""
import argparse
import json
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]


def inspect(unity=False):
    problems = []
    for path in ('ros2_ws/src/agribot_vehicle_description/launch/configured_ackermann_sim.launch.py',
                 'ros2_ws/src/agribot_hardware_bringup/package.xml',
                 'ros2_ws/src/FAST-LIVO2/src/LIVMapper.cpp',
                 'ros2_ws/src/agribot_farm_sim/config/compatibility.json',
                 'unity/ProjectSettings/ProjectVersion.txt'):
        if not (ROOT / path).is_file(): problems.append('缺少源码：' + path)
    source = ROOT / 'ros2_ws/src/agribot_vehicle_description/generated'
    catalog = ROOT / 'unity/Assets/AgribotConfig/Resources/farm_vehicle_presets.json'
    if catalog.exists():
        data = json.loads(catalog.read_text())
        missing = [p['id'] for p in data['presets'] if not (source / p['id'] / 'vehicle_config.json').exists()]
        if missing and unity: problems.append('未附带的模型配置：' + ', '.join(missing))
    if unity:
        for folder in ('Assets/StreamingAssets/LocalFarmExports',
                       'Assets/AgribotConfig/Resources/LocalFarmAssets',
                       'Assets/AgribotConfig/Resources/LocalGarageThumbnails',
                       'Assets/AgribotConfig/Resources/LocalGarageVehicles',
                       'Assets/AgribotConfig/Resources/LocalVehicleAppearances',
                       'Assets/StreamingAssets/LocalVehicleAppearances'):
            if not (ROOT / 'unity' / folder).is_dir():
                problems.append('Unity 完整车库缺少资源：' + folder)
        if catalog.exists():
            missing_models = [p['id'] for p in data['presets']
                              if not (source / p['id'] / 'models' / (p['id'] + '.sdf')).is_file()]
            if missing_models:
                problems.append('仅有参数、没有完整仿真模型的预设：' + ', '.join(missing_models))
    for path in ROOT.rglob('*.fbx'):
        if {'.git', 'Library'} & set(path.parts): continue
        with path.open('rb') as stream:
            if stream.read(80).startswith(b'version https://git-lfs.github.com/spec/v1'):
                problems.append('未下载 LFS 对象，请 git lfs pull：' + str(path.relative_to(ROOT)))
    return problems


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', action='store_true')
    args = parser.parse_args()
    issues = inspect(args.unity)
    print(json.dumps({'ok': not issues, 'issues': issues}, ensure_ascii=False, indent=2))
    return 1 if issues else 0


if __name__ == '__main__':
    sys.exit(main())
