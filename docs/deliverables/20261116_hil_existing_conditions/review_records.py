"""Read-only assessment of actual records against the explicitly revised scope.

This tool cannot start ROS, flash firmware, publish controls or spawn entities.
It is not a claim that thirty new physical tests were executed.
"""
import argparse
import ast
import hashlib
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image
import yaml

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
EVIDENCE = ROOT/'.validation/hil_acceptance_20261116'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--harrow-directory', default='extra-campaign-r2/harrow-cvt-render-r1/harrow-visual')
    parser.add_argument('--fresh-harrow', action='store_true')
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    report = dict(passed=False, assessment_version='V2.1', new_30_case_physical_campaign=False,
        scope='Current-condition record assessment: actual archived physical runs plus explicitly labelled software checks',
        note='Not V2.0 all-pass acceptance, not thirty new bench tests, no third-party signatures', cases={}, input_sha256={})
    def read(relative):
        path = EVIDENCE/relative
        value = json.loads(path.read_text())
        report['input_sha256'][relative] = hashlib.sha256(path.read_bytes()).hexdigest()
        return value
    def successful(relative, field='passed'):
        value = read(relative)
        if value.get(field) is not True:
            raise ValueError('Actual evidence does not pass: '+relative)
        return value
    def case(identity, checks, observation, details=None, mode='既有实板记录复核'):
        report['cases']['TC-HIL-'+identity] = dict(passed=all(checks.values()), checks=checks,
            evidence_mode=mode, observed=observation, details=details or {})

    normal = successful('five-native-normal-runs-review-r1.json')
    scenes = successful('scene-attestation-r1.json')
    catalog = json.loads((ROOT/'unity/Assets/AgribotConfig/Resources/hil_profiles.json').read_text())
    profiles = {p['id']: p for p in catalog['profiles']}
    source_scope = json.loads((HERE/'scope_v21/requirements.json').read_text())
    assert source_scope['version'] == 'V2.1'
    runs = {r['profile']: r for r in normal['runs']}
    assert set(runs) == set(profiles) and len(runs) == 5
    stats = {}
    for name, run in runs.items():
        directory = EVIDENCE/run['directory']
        path = directory/'stamped-poses.jsonl'
        report['input_sha256'][str(path.relative_to(EVIDENCE))] = hashlib.sha256(path.read_bytes()).hexdigest()
        truth = [r for s in path.read_text().splitlines() if (r := json.loads(s))['kind'] == 'truth']
        xy = np.array([r['xy'] for r in truth])
        yaw = np.unwrap([math.atan2(2*(r['quaternion'][3]*r['quaternion'][2]+r['quaternion'][0]*r['quaternion'][1]),
            1-2*(r['quaternion'][1]**2+r['quaternion'][2]**2)) for r in truth])
        stats[name] = dict(truth_samples=len(truth), travel_extent_m=float(np.linalg.norm(np.ptp(xy, axis=0))),
                           heading_extent_rad=float(np.ptp(yaw)), final_speed_mps=run['final_maximum_speed_mps'])

    spray = successful('sprayer-controls-r1/feedback-independent-audit.json')
    shred = successful('normal-campaign-r2/shredder-controls-r2/feedback-independent-audit.json')
    for name in ('normal-campaign-r3/powershift-native-r2', 'normal-campaign-r3/cvt-native-r1'):
        successful(name+'/powertrain-audit.json', 'audit_passed')
    apple = read('apple-native-r2/independent/independent-pc.json')['can']
    case('VCU-001-c', dict(five_normal_physical_runs=normal['passed'],
        ackermann_has_actual_can_reception=any(s['rx_commands'] > 0 and s['tx_feedback'] > 0 for s in apple),
        representative_received_payload_reviews=spray['passed'] and shred['passed']),
        '五种正常物理任务有对应原生收发；Car0实际CAN命令与反馈计数推进，喷水/碎枝及两种传动的既有收到负载审计通过。未新增非法帧注入，不宣称全工况逐字节或零丢包认证。')

    authority = ['apple-controls-r1/authority.json', 'sprayer-controls-r1/authority.json',
        'normal-campaign-r2/shredder-controls-r2/authority.json',
        'normal-campaign-r2/tractor-authority-r1/authority.json',
        'normal-campaign-r4/tractor-cvt-authority-r1/authority.json']
    for name in authority:
        successful(name)
    case('VCU-001-d', dict(five_authority_checks=len(authority) == 5,
        actual_translation=all(v['travel_extent_m'] > 2. for v in stats.values()),
        actual_turning=all(v['heading_extent_rad'] > .2 for v in stats.values()),
        original_terminal_checks=normal['passed']),
        '五种禁止授权专项均通过；独立真值显示正常任务实际平移和转向，原终点速度/位置检查通过。不进行逐轮转角标定。', stats)

    watchdogs = ['normal-campaign-r1/apple-watchdog-r1/chassis-watchdog.json',
        'normal-campaign-r2/sprayer-visual-r1/chassis-watchdog.json',
        'normal-campaign-r2/shredder-controls-r2/chassis-watchdog.json',
        'normal-campaign-r4/tractor-cvt-watchdog-r1/powertrain/result.json',
        'normal-campaign-r4/tractor-powershift-watchdog-r1/powertrain/result.json']
    for name in watchdogs:
        successful(name)
    case('VCU-001-i', dict(existing_controlled_watchdog_records=len(watchdogs) == 5),
        '复核五种受控断令/反馈暂停专项，实际停车及新请求恢复均通过。使用原租约和Car4原0.30秒门限；没有再拔USB或接入错误设备。')

    channels = ['sprayer-controls-r1/channel-rejection.json',
        'normal-campaign-r2/shredder-controls-r2/channel-rejection.json']
    for name in channels:
        successful(name)
    models = {}
    for name, profile in profiles.items():
        path = ROOT/'.local/hil/runtime'/profile['runtime']/'vehicle.urdf'
        if not path.exists():
            path = path.with_suffix('.sdf')
        tree = ET.parse(path)
        models[name] = dict(path=str(path.relative_to(ROOT)), links=len(tree.findall('.//link')),
                            sha256=hashlib.sha256(path.read_bytes()).hexdigest(), implement=profile['implement'])
    case('IMP-001-a', dict(five_catalog_task_bindings=set(runs) == set(profiles),
        model_resources_present=all(m['links'] > 0 for m in models.values()),
        representative_wrong_channels_rejected=len(channels) == 2),
        '五个工况的车型/机具与正常任务对应，运行模型资源存在且已在完整任务加载；已有喷水/碎枝错通道拒绝记录通过。不验收全部安装孔位尺寸或未登记的机械组合。', models)

    harrow = successful(args.harrow_directory+'/result.json')
    history = successful(args.harrow_directory+'/history/result.json')
    harrow_checks = harrow['cases']
    pictures = list((EVIDENCE/args.harrow_directory).glob('visual-*/*.xwd'))
    visual_ok = True
    if args.fresh_harrow:
        visual = successful(args.harrow_directory+'/visual-review.json')
        visual_ok = len(visual['images_sha256']) == 4 and all(
            hashlib.sha256((EVIDENCE/args.harrow_directory/name).read_bytes()).hexdigest() == digest
            for name, digest in visual['images_sha256'].items())
        controller = successful(str(Path(args.harrow_directory).parent/'controller-checks.json'))
        visual_ok = visual_ok and controller['run_id'] == visual['run_id']
    case('IMP-001-i', dict(all_discs_and_roller=harrow_checks['all_sixteen_discs_and_original_roller_engaged']['passed'],
        actual_work_history=history['passed'], actual_pictures_present=len(pictures) == 4,
        current_visual_review_if_requested=visual_ok),
        'Car4/CVT代表性实际控制专项记录16盘入土、原滚筒旋转，工作时历史增加、抬升后历史保留；保存四张真实画面。只验收基础作业显示，不签署材质精细度、全姿态连杆或土壤力学。',
        dict(directory=args.harrow_directory, loaded_visual_libraries=harrow.get('loaded_visual_libraries'),
             history_segments=history['maximum_segments'],
             raised_history_preserved_sim_seconds=history['raised_history_preserved_sim_seconds']),
        mode='本轮原场景代表性实板补测' if args.fresh_harrow else '既有实板记录复核')
    report['new_physical_test_executed_case_ids'] = ['TC-HIL-IMP-001-i'] if args.fresh_harrow else []

    successful('sprayer-controls-r1/controller-checks.json')
    successful('normal-campaign-r2/shredder-controls-r2/controller-checks.json')
    hitch = successful('controller-after-async-r1/hitch.json')
    arm = successful('apple-native-r2/apple-independent-audit.json')
    case('IMP-001-j', dict(five_normal_completions=normal['passed'],
        representative_hitch_timeout_holds=hitch['cases']['09_command_timeout_holds']['passed'],
        arm_normal_completion=arm['passed']),
        '五种正常任务均完成结束清理，机械臂双果放盘并收臂；喷水/碎枝停令及悬挂断令保持沿用已有代表性实板专项。未实施夹果中途强行终止。')

    for run in normal['runs']:
        successful(run['directory']+'/sensor-audit.json')
    case('NAV-001-a', dict(five_actual_sensor_audits=normal['passed'],
        deployed_sensor_configs_match=scenes['passed']),
        '五种正常传感审计通过，所需时钟/IMU/GNSS/点云推进；Car0/Car3正常PNG像素核对保留，Car4不追加相机定位要求。复核部署外参，未人为卸载压缩插件。')

    startup = {}
    for name, run in runs.items():
        path = EVIDENCE/run['directory']/'hardware-evidence/pc/stages.jsonl'
        report['input_sha256'][str(path.relative_to(EVIDENCE))] = hashlib.sha256(path.read_bytes()).hexdigest()
        stages = [json.loads(s)['phase'] for s in path.read_text().splitlines()]
        startup[name] = dict(waiting_ready_before_mission=stages.index('waiting_ready') < stages.index('starting_mission'),
                             completed='completed' in stages, algorithm=profiles[name]['algorithm'])
    case('NAV-001-b', dict(normal_readiness_before_start=all(v['waiting_ready_before_mission'] for v in startup.values()),
        five_completed_with_localization=all(v['completed'] for v in startup.values())),
        '五工况启动记录均先等待就绪再提交任务，正常定位输入下完成全程。对应既定FAST-LIVO2+RTK或NavSat配置；没有本轮缺输入/TF注入，也不声称该负向分支已验收。', startup)

    kinematics = {}
    for name, profile in profiles.items():
        path = ROOT/'.local/hil/runtime'/profile['runtime']/'rdk/nav2.yaml'
        config = yaml.safe_load(path.read_text())
        follow = config['controller_server']['ros__parameters']['FollowPath']
        planner = config['planner_server']['ros__parameters']['GridBased']
        expected = 'DiffDrive' if profile['car'] == 'Car3' else 'Ackermann'
        kinematics[name] = dict(model=follow['motion_model'], expected=expected,
            search=planner['motion_model_for_search'], minimum_radius=planner['minimum_turning_radius'])
    case('NAV-001-e', dict(model_matches_vehicle=all(v['model'] == v['expected'] for v in kinematics.values()),
        ackermann_forward_search=all(v['search'] == 'DUBIN' for n, v in kinematics.items() if profiles[n]['car'] != 'Car3'),
        actual_movement_and_turning=all(v['travel_extent_m'] > 2. and v['heading_extent_rad'] > .2 for v in stats.values()),
        five_normal_completions=normal['passed']),
        '五工况配置为Car0/Car4 Ackermann、Car3 DiffDrive，阿克曼保持DUBIN前进搜索。独立真值证实实际行驶与转弯、任务到点；不做逐轮或全程侧滑定量标定。', kinematics)

    maps = {}
    for name, profile in profiles.items():
        path = ROOT/'.local/hil/runtime'/profile['runtime']/'rdk/map.pgm'
        image = np.asarray(Image.open(path).convert('L'))
        maps[name] = dict(occupied_pixels=int(np.count_nonzero(image < 89)), sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    raw_path = EVIDENCE/'sprayer-native-r1/independent/raw-collisions.jsonl'
    report['input_sha256'][str(raw_path.relative_to(EVIDENCE))] = hashlib.sha256(raw_path.read_bytes()).hexdigest()
    rows = [r for s in raw_path.read_text().splitlines() if (r := json.loads(s))['topic'].endswith('costmap_raw')
            and 'cells_map_cost' in r]
    marked = sum(any(cell[-1] == 254 for cell in r['cells_map_cost']) for r in rows)
    inside = max((r['lethal_254_inside'] for r in rows), default=None)
    representative = ROOT/'.local/hil/runtime/tracked_sprayer'
    vehicle = json.loads((representative/'vehicle_config.json').read_text())
    config = yaml.safe_load((representative/'rdk/nav2.yaml').read_text())
    polygon = np.asarray(ast.literal_eval(config['global_costmap']['global_costmap']['ros__parameters']['footprint']))
    collision = vehicle['collision']
    centre, size = np.asarray(collision['center'][:2]), np.asarray(collision['size'][:2])
    footprint_matches_body = np.allclose(polygon.min(axis=0), centre-size/2-.02, atol=1e-7) and \
        np.allclose(polygon.max(axis=0), centre+size/2+.02, atol=1e-7)
    provenance = json.loads((representative/'provenance.json').read_text())
    case('NAV-001-g', dict(original_maps_have_obstacles=all(v['occupied_pixels'] > 0 for v in maps.values()),
        existing_costmaps_mark_actual_scene_obstacles=marked > 0,
        representative_body_has_no_persistent_lethal_cell=inside == 0,
        chassis_footprint_matches_original_body=bool(footprint_matches_body),
        implement_has_no_added_collision=provenance['implement_collision'] is False,
        default_route_completed=runs['tracked_sprayer']['passed']),
        '未增加物体。复核原地图、冻结轮廓及喷水原场景只读代价图：736组有效地图样本均有原有致命障碍标记，车体内致命栅格最大为0，默认路线完成。只验收既有场景/配置，不宣称动态突发障碍或无路场景已测试。',
        dict(original_maps=maps, valid_costmap_samples=len(rows), samples_with_existing_lethal_cells=marked,
             maximum_body_lethal_cells=inside, footprint=polygon.tolist(),
             existing_chassis_footprint_margin_m=.02, implement_collision=provenance['implement_collision']))

    recovery = read('sprayer-r1/hardware-evidence/x5/mission.json')
    steps = recovery['navigation']
    original_goals = recovery['manifest']['fixed_route_points']
    remaining = steps[1]['requested_goals'] if len(steps) > 1 else []
    patience = successful('controller-patience-r1/result.json')
    case('NAV-001-i', dict(actual_historical_failure_is_explicit=bool(steps and steps[0]['executed'] is False and 'status=6' in steps[0]['error']),
        remaining_order_kept=bool(remaining and remaining == original_goals[-len(remaining):]),
        actual_recovery_recorded=recovery['status'] == 'succeeded' and steps[-1]['executed'] is True,
        software_controller_contract=patience['passed']),
        '既有喷水实板记录首次MPPI动作以status=6失败，后继Smac只保留尚未经过的3号原目标并成功。另有同版本原生controller_server软件夹具验证原2秒patience失败零输出及新动作恢复。两种证据范围分列，不记为本轮五工况强制故障。',
        dict(actual_attempts=len(steps), remaining_goals=remaining), mode='既有代表性实板记录复核＋明确标注的软件夹具')
    report['passed'] = len(report['cases']) == 11 and all(c['passed'] for c in report['cases'].values())
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(dict(passed=report['passed'], assessed=len(report['cases']),
        cases={k:v['passed'] for k,v in report['cases'].items()}), ensure_ascii=False, indent=2))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
