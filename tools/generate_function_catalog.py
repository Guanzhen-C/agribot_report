#!/usr/bin/env python3
"""Generate documentation tables from the actual simulation configuration."""
import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'ros2_ws/src'
sys.path[:0] = [str(SRC / 'agribot_farm_sim'), str(SRC / 'agribot_vehicle_config_tools')]
from agribot_farm_sim.compatibility import read_catalog, select_algorithm
from agribot_farm_sim.natural_recommendation import EXCLUSIONS


def cell(value):
    return str(value).replace('|', '\\|').replace('\n', ' ')


def table(lines, headers, rows):
    lines += ['| ' + ' | '.join(headers) + ' |', '| ' + ' | '.join(['---'] * len(headers)) + ' |']
    lines += ['| ' + ' | '.join(cell(value) for value in row) + ' |' for row in rows]
    lines.append('')


def render():
    config_path = SRC / 'agribot_farm_sim/config/compatibility.json'
    scenarios_path = SRC / 'agribot_farm_sim/config/scenarios.json'
    data = read_catalog(config_path)
    scenarios = json.loads(scenarios_path.read_text())['scenarios']
    scene_names = {row['id']: row['name'] for row in scenarios}
    groups = data['sceneGroups']
    vehicles = {row['car']: row for row in data['vehicles']}
    mounts = defaultdict(list)
    for row in data['bindings']:
        if row['implement'] != 'none':
            mounts[row['car']].append(row['name'])
    lines = ['# 车型、装备、场景与算法配置附录', '',
             '本文件由 `tools/generate_function_catalog.py` 从当前配置生成，避免手写目录与软件脱节。', '',
             '**这是配置目录和候选适配关系，不是所有组合的全程验收报告。** 机具名称不表示已经模拟真实耕作、播种、喷洒、切割或采摘。', '',
             '## 1. 车型与可挂载机具', '']
    table(lines, ['编号', '名称', '运动学', '配置ID', '机具目录（不含裸车）'],
          [(row['car'], row['name'], '阿克曼' if row['type'] == 'ackermann' else '差速/履带',
            '`' + row['id'] + '`', '、'.join(dict.fromkeys(mounts[row['car']])) or '无') for row in data['vehicles']])
    lines += ['## 2. 内置默认场景', '', '用户导入场景为本机动态内容，不编造进本附录的固定清单。', '']
    table(lines, ['ID', '名称', '目录任务标签'], [(s['id'], s['name'], '、'.join(s.get('tasks', []))) for s in scenarios])
    lines += ['任务标签只说明场景目录用途，不证明对应物理作业已实现。', '', '## 3. 五组算法组件', '']
    labels = {'perception': '几何感知', 'vision': '额外视觉感知', 'localization': '定位', 'planner': '规划', 'controller': '控制'}
    table(lines, ['类别', '当前组件ID'], [(labels[key], '、'.join('`' + name + '`' for name in values)) for key, values in data['components'].items()])
    lines += ['## 4. 挂载预设及候选场景组', '',
              '候选组不是已通过白名单。AI还会检查排除规则、配置版本和方案条件。JSON存在不代表完整模型已经随仓库提供。', '']
    rows = []
    for binding in data['bindings']:
        car = vehicles[binding['car']]
        group = binding.get('sceneGroup', car['sceneGroup'])
        allowed = [scene_names[name] for name in groups[group] if (car['car'], name) not in EXCLUSIONS]
        preset = binding['preset']
        model = SRC / 'agribot_vehicle_description/generated' / preset / 'models' / (preset + '.sdf')
        rows.append((car['car'], binding['name'], preset, '、'.join(allowed), '已包含' if model.is_file() else '需补资源'))
    table(lines, ['车型', '挂载', '导出预设', '候选场景（非验收）', '仓库SDF'], rows)
    lines += ['## 5. 默认配方与定位限制', '',
              '以下按各车裸车绑定调用当前 `select_algorithm` 解析。绑定级机具策略仍以完整配置为准；该API给出测试/推荐配方，不自行切换运行中的算法。', '']
    rows = []
    for car in data['vehicles']:
        binding = next(row for row in data['bindings'] if row['car'] == car['car'] and row['implement'] == 'none')
        for scene in scenarios:
            if scene['id'] not in groups[car['sceneGroup']] or (car['car'], scene['id']) in EXCLUSIONS:
                continue
            recipe = select_algorithm(data, binding, scene['id'])
            parts = recipe['components']
            rows.append((car['car'], scene['name'], parts['localization'], parts['planner'], parts['controller'],
                         '、'.join(recipe.get('allowedLocalizationModes', [])) or '未追加专用白名单'))
    table(lines, ['车型', '场景', '默认定位', '规划', '控制', '定位专用白名单'], rows)
    lines += ['## 6. 明确排除的推荐组合', '']
    table(lines, ['车型', '场景'], [(car, scene_names[scene]) for car, scene in sorted(EXCLUSIONS)])
    lines += ['上述目录不等于手工菜单对所有组合都提供强制禁用；人工选项仍应遵循场景规则，不能把“可选”当“可用”。', '',
              '## 7. 当前功能限制', '']
    lines += ['- ' + value for value in data['limitations']]
    lines += ['', '## 8. 依据与更新', '',
              '- 配置：`ros2_ws/src/agribot_farm_sim/config/compatibility.json`',
              '- 场景：`ros2_ws/src/agribot_farm_sim/config/scenarios.json`',
              '- 解析：`compatibility.py::select_algorithm`，排除：`natural_recommendation.py::EXCLUSIONS`',
              '- 更新：`python3 tools/generate_function_catalog.py`；只读校验：`python3 tools/generate_function_catalog.py --check`', '',
              '配置SHA256：`' + hashlib.sha256(config_path.read_bytes()).hexdigest() + '`', '',
              '场景SHA256：`' + hashlib.sha256(scenarios_path.read_bytes()).hexdigest() + '`', '']
    return '\n'.join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    path = ROOT / 'docs/14_catalog_reference.md'
    content = render()
    if args.check:
        if not path.is_file() or path.read_text() != content:
            raise SystemExit('Catalog documentation is out of date')
        print('Catalog documentation matches the configuration.')
    else:
        path.write_text(content)
        print(path)


if __name__ == '__main__':
    main()
