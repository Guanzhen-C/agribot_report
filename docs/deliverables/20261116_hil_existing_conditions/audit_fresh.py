"""Case-level V2.1 verdicts bound to explicitly selected fresh physical runs."""
import argparse
import ast
from collections import Counter
import datetime
import hashlib
import json
import math
from pathlib import Path
import sys

import numpy as np
from PIL import Image
import yaml

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
PROFILES = ('ackermann_apple', 'tracked_sprayer', 'tracked_shredder', 'tractor_powershift', 'tractor_cvt')
REVISED = {'VCU-c', 'VCU-d', 'VCU-i', 'IMP-a', 'IMP-i', 'IMP-j',
           'NAV-a', 'NAV-b', 'NAV-e', 'NAV-g', 'NAV-i'}


def fresh_identity(identity, after):
    start = datetime.datetime.strptime(identity.split('_')[0], '%Y%m%dT%H%M%S').replace(tzinfo=datetime.timezone.utc)
    if start < after:
        raise ValueError('Archived run cannot stand in for a fresh execution: '+identity)


def verify_visual_review(directory, review, run_id, count):
    if review.get('run_id') != run_id:
        raise ValueError('Visual review belongs to a different physical run')
    images, originals = review['images_sha256'], review['original_capture_sha256']
    if len(images) != count or set(originals) != {str(Path(p).with_suffix('.xwd')) for p in images}:
        raise ValueError('Required images and their original captures are incomplete')
    paths = []
    for entries in (images, originals):
        for name, expected in entries.items():
            path = (directory/name).resolve()
            if not path.is_relative_to(directory.resolve()) or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
                raise ValueError('Reviewed visual evidence changed: '+name)
            paths.append(path)
    return paths


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, required=True)
    parser.add_argument('--phase', choices=('a', 'b'), required=True)
    parser.add_argument('--auxiliary', type=Path, required=True)
    parser.add_argument('--supplement', type=Path)
    parser.add_argument('--selection', type=Path, help='Explicit fresh job directories when a failed campaign was resumed')
    parser.add_argument('--after', required=True, help='UTC lower bound for every new physical run')
    parser.add_argument('--historical-recovery', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    after = datetime.datetime.fromisoformat(args.after.replace('Z', '+00:00'))
    directory, auxiliary = args.directory.resolve(), args.auxiliary.resolve()
    requirements_path = HERE/'scope_v21/requirements.json'
    requirements = json.loads(requirements_path.read_text())
    report = dict(passed=False, phase=args.phase, requirements_version='V2.1',
        executed_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        scope='Fresh physical multi-profile sessions with case-level assertions; NAV-i explicitly combines archived failure review and a freshly rerun software fixture',
        fresh_30_case_campaign=args.phase == 'b',
        software_and_historical_case_ids=['TC-HIL-NAV-001-i'],
        physical_session_note='One fresh session may supply observations for multiple cases; this is not thirty independent full-route runs.',
        input_sha256={}, records=[], physical_runs={})
    current_inputs = []

    def raw(path):
        path = path.resolve()
        data = path.read_bytes()
        report['input_sha256'][str(path)] = hashlib.sha256(data).hexdigest()
        current_inputs.append(str(path))
        return data

    def read(path, field=None):
        value = json.loads(raw(path))
        if field and value.get(field) is not True:
            raise AssertionError(str(path)+' does not pass '+field)
        return value

    jobs = json.loads((HERE/('phase_'+args.phase+'_jobs.json')).read_text())
    paths = {(j['profile'], j['kind']): directory/j['name'] for j in jobs}
    if args.selection:
        selected = read(args.selection)
        paths = {(j['profile'],j['kind']):(ROOT/j['directory']).resolve() for j in selected}
        if set(paths) != {(p,k) for p in PROFILES for k in ('mission','controller')} or len(selected) != 10:
            raise ValueError('Resumed selection must explicitly cover exactly ten fresh profile/kind sessions')
    report['selected_run_directories'] = {p+':'+k:str(v) for (p,k),v in paths.items()}
    catalog = {p['id']: p for p in json.loads((ROOT/'unity/Assets/AgribotConfig/Resources/hil_profiles.json').read_text())['profiles']}
    cache = {}
    controller_cache = {}

    def normal(name):
        if name in cache:
            current_inputs.extend(cache[name]['evidence'])
            return cache[name]
        evidence_start = len(current_inputs)
        folder = paths[name, 'mission']
        mission = read(folder/'mission-checks.json', 'passed')
        fresh_identity(mission['run_id'], after)
        if mission['real_files_changed']:
            raise AssertionError('Protected real runtime changed')
        read(folder/'postcheck.json', 'passed')
        read(folder/'runtime-attestation.json', 'passed')
        sensor = read(folder/'sensor-audit.json', 'passed')
        terminal = read(folder/'terminal.json', 'passed')
        pose = read(folder/'pose-audit.json')
        manifest = read(folder/'hardware-evidence/manifest.json')
        if manifest['run_id'] != mission['run_id']:
            raise AssertionError('Archive belongs to another run')
        for relative, row in manifest['files'].items():
            if hashlib.sha256(raw(folder/'hardware-evidence'/relative)).hexdigest() != row['sha256']:
                raise AssertionError('Raw evidence changed: '+relative)
        firmware = read(folder/'hardware-evidence/pc/firmware.json')
        expected_bitrate = 1000000 if name == 'ackermann_apple' else 250000
        if firmware['bitrate'] != expected_bitrate or firmware['id'] != catalog[name]['firmware']:
            raise AssertionError('Actual firmware/profile mismatch')
        flash = raw(folder/'hardware-evidence/pc/flash.log').decode()
        if not all(t in flash for t in ('Programming Finished', 'Verified OK')):
            raise AssertionError('Fresh flash evidence incomplete')
        stages = [json.loads(line)['phase'] for line in raw(folder/'hardware-evidence/pc/stages.jsonl').splitlines()]
        if not stages.index('waiting_ready') < stages.index('starting_mission') < stages.index('running'):
            raise AssertionError('Readiness/authorization order invalid')
        if name == 'ackermann_apple':
            observed = read(folder/'independent/independent-pc.json')
            statuses = observed['can']
        else:
            observed = read(folder/'independent/independent.json')
            statuses = [s[2] for s in observed['samples']['can_status' if name.startswith('tractor_') else 'status']]
            if observed['transport_events']:
                raise AssertionError('Normal run had transport recovery/fault events')
        faults = [s for s in statuses if s.get('fault') or s.get('errors', 0) or s.get('transport_recoveries', 0)]
        if not statuses or faults:
            raise AssertionError('Normal physical CAN data absent or contains a fault')
        plans = sum(len(v) for v in terminal['observed_planner_actions'].values())
        expected_plans = 2 if name == 'ackermann_apple' else 5 if name.startswith('tractor_') else 1
        if plans != expected_plans or terminal['new_planner_goals']:
            raise AssertionError('Unexpected normal/post-goal planning actions')
        if not pose['localization_xy']['samples'] or not pose['path_tracking_xy']['samples']:
            raise AssertionError('Missing independent localization/path measurements')
        if name.startswith('tractor_'):
            read(folder/'powertrain-audit.json', 'audit_passed')
            read(folder/'powertrain-independent-audit.json', 'passed')
        truths = [json.loads(line) for line in raw(folder/'stamped-poses.jsonl').splitlines()
                  if json.loads(line)['kind'] == 'truth']
        xy = np.asarray([r['xy'] for r in truths])
        yaw = np.unwrap([math.atan2(2*(r['quaternion'][3]*r['quaternion'][2]+r['quaternion'][0]*r['quaternion'][1]),
                                   1-2*(r['quaternion'][1]**2+r['quaternion'][2]**2)) for r in truths])
        stats = dict(run_id=mission['run_id'], profile=name, final_error_m=terminal['final_error_m'],
            final_maximum_speed_mps=terminal['maximum_planar_speed_mps'], physical_can_samples=len(statuses),
            normal_planning_actions=plans, travel_extent_m=float(np.linalg.norm(np.ptp(xy, axis=0))),
            heading_extent_rad=float(np.ptp(yaw)), localization_xy=pose['localization_xy'],
            path_tracking_xy=pose['path_tracking_xy'], localization_match_coverage=pose['localization_match_coverage'])
        report['physical_runs'][name] = stats
        cache[name] = dict(folder=folder, mission=mission, sensor=sensor, terminal=terminal, observed=observed,
                           stats=stats, evidence=list(current_inputs[evidence_start:]))
        return cache[name]

    def normals():
        return [normal(p) for p in PROFILES]

    def control(name, file):
        folder = paths[name, 'controller']
        if name not in controller_cache:
            evidence_start = len(current_inputs)
            session = read(folder/'controller-checks.json', 'passed')
            fresh_identity(session['run_id'], after)
            manifest = read(folder/'hardware-evidence/manifest.json')
            if manifest['run_id'] != session['run_id']:
                raise AssertionError('Wrong controller archive')
            for relative, row in manifest['files'].items():
                if hashlib.sha256(raw(folder/'hardware-evidence'/relative)).hexdigest() != row['sha256']:
                    raise AssertionError('Controller raw evidence changed: '+relative)
            read(folder/'runtime-attestation.json', 'passed')
            controller_cache[name] = list(current_inputs[evidence_start:])
        else:
            current_inputs.extend(controller_cache[name])
        value = read(folder/file)
        flag = 'ok' if file in ('sprayer.json', 'shredder.json') else 'passed'
        if value.get(flag) is not True:
            raise AssertionError('Fresh controller check failed: '+str(folder/file))
        return value

    def each_controls(file):
        return [control(p, file) for p in PROFILES]

    def model_and_scene():
        read(auxiliary/'scene-attestation.json', 'passed')
        for p in PROFILES:
            normal(p)
            runtime = ROOT/'.local/hil/runtime'/catalog[p]['runtime']
            model = runtime/'vehicle.urdf'
            if not model.exists():
                model = runtime/'vehicle.sdf'
            if not raw(model):
                raise AssertionError('Empty model')
            config = yaml.safe_load(raw(runtime/'rdk/nav2.yaml'))
            expected = 'DiffDrive' if catalog[p]['car'] == 'Car3' else 'Ackermann'
            if config['controller_server']['ros__parameters']['FollowPath']['motion_model'] != expected:
                raise AssertionError('Wrong MPPI motion model')
            if expected == 'Ackermann' and config['planner_server']['ros__parameters']['GridBased']['motion_model_for_search'] != 'DUBIN':
                raise AssertionError('Ackermann forward search changed')

    def movement():
        for n in normals():
            if n['stats']['travel_extent_m'] <= 2. or n['stats']['heading_extent_rad'] <= .2:
                raise AssertionError('Actual translation/turning was not demonstrated')

    def work_feedback():
        for p, kind in [('tracked_sprayer', 'sprayer'), ('tracked_shredder', 'shredder')]:
            control(p, kind+'.json')
            control(p, 'feedback-independent-audit.json')

    def hitch():
        if args.phase == 'a':
            if args.supplement is None:
                control('tractor_cvt', 'hitch.json')
            else:
                session = read(args.supplement/'a-hitch-hold/controller-checks.json', 'passed')
                fresh_identity(session['run_id'], after)
                read(args.supplement/'a-hitch-hold/hitch.json', 'passed')
        else:
            for p in PROFILES[-2:]:
                control(p, 'hitch.json')

    def arm():
        n = normal('ackermann_apple')
        read(n['folder']/'apple-independent-audit.json', 'passed')
        read(n['folder']/'held-fruit-guard.json', 'passed')

    def harrow():
        control('tractor_cvt', 'harrow-visual/result.json')
        control('tractor_cvt', 'harrow-visual/history/result.json')
        pictures('tractor_cvt', 'harrow-visual', 4)

    def pictures(name, folder, count):
        review = control(name, folder+'/visual-review.json')
        session = read(paths[name, 'controller']/'controller-checks.json', 'passed')
        for path in verify_visual_review(paths[name, 'controller']/folder, review, session['run_id'], count):
            raw(path)

    def spray_pictures():
        control('tracked_sprayer', 'spray-existing/result.json')
        pictures('tracked_sprayer', 'spray-existing', 3)

    def watchdogs():
        for p in PROFILES:
            control(p, 'powertrain/result.json' if p.startswith('tractor_') else 'chassis-watchdog.json')

    def obstacle():
        n = normal('tracked_sprayer')
        rows = [json.loads(line) for line in raw(n['folder']/'independent/raw-collisions.jsonl').splitlines()]
        maps = [r for r in rows if r['topic'].endswith('costmap_raw') and 'cells_map_cost' in r]
        if not maps or not any(any(c[-1] == 254 for c in r['cells_map_cost']) for r in maps):
            raise AssertionError('No existing lethal scene cells recorded')
        if any(r['lethal_254_inside'] for r in maps):
            raise AssertionError('Lethal cells were observed inside the chassis; investigate raw samples')
        runtime = ROOT/'.local/hil/runtime/tracked_sprayer'
        vehicle = read(runtime/'vehicle_config.json')
        provenance = read(runtime/'provenance.json')
        config = yaml.safe_load(raw(runtime/'rdk/nav2.yaml'))
        polygon = np.asarray(ast.literal_eval(config['global_costmap']['global_costmap']['ros__parameters']['footprint']))
        center, size = np.asarray(vehicle['collision']['center'][:2]), np.asarray(vehicle['collision']['size'][:2])
        if provenance['implement_collision'] is not False or not np.allclose(polygon.min(axis=0), center-size/2-.02, atol=1e-7) or not np.allclose(polygon.max(axis=0), center+size/2+.02, atol=1e-7):
            raise AssertionError('Original body-only planning footprint changed')
        occupied = int(np.count_nonzero(np.asarray(Image.open(runtime/'rdk/map.pgm').convert('L')) < 89))
        if not occupied:
            raise AssertionError('Original map obstacles absent')
        report['existing_obstacle_observations'] = dict(samples=len(maps), maximum_inside_lethal_cells=0, occupied_map_pixels=occupied)

    def recovery():
        historic = read(args.historical_recovery)
        steps = historic['navigation']
        remaining = steps[1]['requested_goals']
        original = historic['manifest']['fixed_route_points']
        if steps[0]['executed'] is not False or 'status=6' not in steps[0]['error'] or not remaining or remaining != original[-len(remaining):] or historic['status'] != 'succeeded':
            raise AssertionError('Archived failure/ordered remaining-goal recovery does not satisfy revised NAV-i')
        fresh = read(auxiliary/'controller-patience/result.json', 'passed')
        if fresh['hardware_outputs'] is not False or fresh['controller_patience_seconds'] != 2. or not all(fresh['checks'].values()):
            raise AssertionError('Fresh software controller fixture incomplete')

    actions = {
        'VCU-c': lambda: (normals(), work_feedback()),
        'VCU-d': lambda: (each_controls('authority.json'), movement()),
        'VCU-i': watchdogs,
        'IMP-a': lambda: (model_and_scene(), control('tracked_sprayer', 'channel-rejection.json'), control('tracked_shredder', 'channel-rejection.json')),
        'IMP-i': harrow,
        'IMP-j': lambda: (normals(), arm(), work_feedback(), hitch()),
        'NAV-a': lambda: (normals(), model_and_scene()),
        'NAV-b': lambda: (normals(), model_and_scene()),
        'NAV-e': lambda: (model_and_scene(), movement()),
        'NAV-g': obstacle,
        'NAV-i': recovery,
        'VCU-a': lambda: (normals(), read(auxiliary/'flash-repeat-cancel/qualification.json', 'passed'), read(auxiliary/'negative-preflight/result.json', 'passed')),
        'VCU-b': lambda: (normals(), read(auxiliary/'interfaces-1m.json', 'passed'), read(auxiliary/'interfaces-250k.json', 'passed')),
        'VCU-e': lambda: (normal('tractor_powershift'), control('tractor_powershift', 'powertrain/result.json')),
        'VCU-f': lambda: (normal('tractor_cvt'), control('tractor_cvt', 'powertrain/result.json')),
        'VCU-g': lambda: (control('tractor_powershift', 'parameters/result.json'), control('tractor_cvt', 'parameters/result.json')),
        'VCU-h': lambda: (normal('tractor_powershift'), normal('tractor_cvt')),
        'VCU-j': normals,
        'IMP-b': lambda: (arm(), control('ackermann_apple', 'arm-negatives/result.json')),
        'IMP-c': lambda: (arm(), control('ackermann_apple', 'arm-negatives/result.json')),
        'IMP-d': arm,
        'IMP-e': lambda: (normal('tracked_sprayer'), control('tracked_sprayer', 'sprayer.json'), control('tracked_sprayer', 'feedback-independent-audit.json')),
        'IMP-f': spray_pictures,
        'IMP-g': lambda: (normal('tracked_shredder'), control('tracked_shredder', 'shredder.json'), control('tracked_shredder', 'channel-rejection.json'), control('tracked_shredder', 'feedback-independent-audit.json')),
        'IMP-h': hitch,
        'NAV-c': model_and_scene,
        'NAV-d': normals,
        'NAV-f': normals,
        'NAV-h': normals,
        'NAV-j': lambda: (normals(), arm()),
    }

    def describe(key):
        descriptions = {
            'VCU-a': '五工况分别重新启动并实际烧录、校验对应固件；同一工况连续两次在烧录中请求取消，均在写入/校验完成后清理，未进入任务。错误组合、算法、传感器及烧录器绑定预检实际拒绝。',
            'VCU-b': '新完整任务分别取得两段实体CAN反馈后就绪；Car0为1 Mbit/s，其余为250 kbit/s。另重新读取实际接口身份/时序，错误身份、速率及接口均拒绝，拒绝检查没有新增TX。',
            'VCU-c': '五工况重新取得物理CAN命令、反馈及实际运动/机具结果；喷水、碎枝收到的原生负载与X5物理ISOBUS及ROS返回三方核对一致，两种传动四类反馈审计通过。未推断零丢包。',
            'VCU-d': '五工况均在授权参数实际读回false时提交非零请求，车体保持静止；授权后的完整路线实际发生平移和转向，结束按原门限停车。没有直接修改车体位置。',
            'VCU-e': '动力换挡完整五阶段及受控专项通过；实际完成起步、升挡、降挡和离合器交接。正常任务原动力审计与独立PC交叉审计均通过，没有带速非预期PT_OFF。',
            'VCU-f': 'CVT完整五阶段及受控速比/负载/变化率专项通过；正常任务原动力与独立PC审计均通过，无非预期退出CVT；终态退出驱动并制动。全部测量值保留在原始状态及曲线中。',
            'VCU-g': '两种选定传动工况均重新进行5组合法边界和17种非法参数请求；实际应答、使能关闭/制动及拒绝记录通过，运行中换模式拒绝和CVT变化率专项通过。',
            'VCU-h': '两种传动新完整任务中的0x642至0x645与PGN FF74至FF77逐类收到负载核对通过，实际挡位、速比、离合器及制动可还原。收到及匹配数量独立保存，不等同于零丢包。',
            'VCU-i': '五工况重新执行受控断令及隔离反馈进程暂停专项，实际零执行/制动、停稳及新的合法请求恢复均通过；没有重放旧正目标，Car4仍使用原0.30秒门限。',
            'VCU-j': '五个新完整任务、独立CAN稳定性、终点观察和关闭后的证据审查均通过，真车保护文件未变。正常轮与专项故障注入分开记录，之前失败轮不覆盖。',
            'IMP-a': '五种车型/机具/任务绑定及运行模型重新核对，资源实际加载；喷水和碎枝均重新执行错通道拒绝及新合法请求恢复，不能误启动未挂载通道。',
            'IMP-b': '新双果任务取得两颗不同的可执行目标，实际规划、双侧果梗接触及完成审计通过；另重新执行不可达、无效关节、不完整路径及超时拒绝检查，没有通过跳过碰撞检查完成。',
            'IMP-c': '新任务行驶夹爪保持、停稳采摘及约束切换审计通过；专项对运动底盘、陈旧关节和缺少模式应答均拒绝，夹持中错误保持请求被拒绝。实际指尖误差见逐段原始审计，仍按0.25 mm上限。',
            'IMP-d': '新连续任务中两颗不同苹果各采一次；两侧夹持垫对同一果梗的接触、摘离、放盘、收臂及驶至3号点后两果留在原果盒内均由独立记录核对通过。',
            'IMP-e': '重新提交风机3挡/水泵4挡并有限行走，PC实际收到(3,4,0)及行走报文，实际风机和水泵旋转后减速停止；0x5B2/0x5B3的三方收到负载审查通过。',
            'IMP-f': '新实际水泵/CAN联动、原8喷头深度网格、原场景非地面障碍截断、停泵严格零发射及既有粒子寿命检查通过；真实多视角画面另行审阅。没有加入测试方块或额外喷嘴。',
            'IMP-g': '重新提交碎枝控制量3500，PC实际收到(0,0,3500)，刀轴实际旋转并在停令后停止；错误喷水通道拒绝，0x5B4三方收到负载审查通过。3500不是转速命令。',
            'IMP-h': '新悬挂专项重新完成升、降、位置、保持、限位、断令保持及新请求恢复；0x630/0x631形成实际闭环，完整任务按原0/9000目标和150计数到位门限完成。',
            'IMP-i': 'CVT代表工况重新下放前进，16片圆盘入土、原滚筒旋转，工作时作业段增长；抬升后历史保留且不新增。保存并实际审阅4张新Gazebo画面及所加载显示库标识。',
            'IMP-j': '五种新正常任务结束状态通过，机械臂放果收臂；新喷水/碎枝单通道断令实际减速且合法行走继续，新悬挂断令保持专项通过。没有夹果时强制中断或张爪。',
            'NAV-a': '五工况重新采集正常网口传感器和时钟推进记录，部署外参/输入目录核对通过；Car0/Car3的PNG原图与解码严格像素审计通过，Car4按RTK/IMU检查，不追加相机要求。',
            'NAV-b': '五组新启动阶段均先等待定位/反馈/Nav2就绪，再授权和提交默认任务，完整路线完成。Car0/Car3保留FAST-LIVO2+RTK，Car4保留NavSat ESKF，没有用真值替代定位。',
            'NAV-c': '重新核对两端地图像素、任务、导航及外参的原始/明确路径归一化哈希；原出生点、目标顺序和轴距未变。Car3只选择修正限幅的PC HIL执行插件，不改变几何、规划轮廓或X5算法。',
            'NAV-d': '五个新完整任务的正常Smac规划动作数分别为2、1、1、5、5，均由原生MPPI执行；无周期全局重规划，终点后也没有新增规划。',
            'NAV-e': '重新核对Car0/Car4 Ackermann、Car3 DiffDrive及阿克曼DUBIN前进搜索；五个完整任务的独立实际轨迹均有直行、转弯和到点，没有瞬移替代控制。',
            'NAV-f': '五组新完整轨迹分别按消息仿真时戳匹配定位、真值和执行路径，输出定位XY、折线跟踪和终点误差，以及样本数、覆盖率、P95/最大值和未匹配样本。未跨机相减墙钟。',
            'NAV-g': '新喷水原场景完整路线与只读代价图核查通过，原地图障碍仍在，底盘原轮廓未缩小且机具不扩入轮廓。没有新增障碍、清除原障碍或关闭避障。',
            'NAV-h': '五种新连续任务均按原顺序完成：采摘双果后到3号点、喷水/碎枝1→2→3、两种传动五阶段作业/抬升转弯/停车。正常阶段规划数及CAN稳定性检查通过，无未解释卡停。',
            'NAV-i': '重新执行同版本原生controller_server软件夹具：原2秒patience后失败终态6、零输出、新动作恢复及取消确认均通过；另按V2.1核对指定历史实板失败及只保留原剩余3号目标的恢复记录，不冒充新实板故障注入。',
            'NAV-j': '五工况重新完成终点后5秒仿真窗口，实际位置在各自原半轴距内，速度低于原0.02/0.03 m/s门限，无新增规划；采摘仍单独使用0.10 m/0.15 rad专用条件。',
        }
        text = descriptions[key]
        if key in ('NAV-j', 'VCU-j'):
            text += ' 本轮终点误差(m)：'+', '.join(p+'='+format(cache[p]['stats']['final_error_m'], '.4f') for p in PROFILES)+'。'
        if key == 'NAV-g':
            text += ' 有效代价图样本'+str(report['existing_obstacle_observations']['samples'])+'组，车体内致命栅格最大0。'
        return text
    for section in requirements['sections']:
        group = section['test_prefix'].split('-')[2]
        for case in section['cases']:
            key = group+'-'+case['letter']
            if args.phase == 'a' and key not in REVISED:
                continue
            current_inputs.clear()
            row = dict(id=section['test_prefix']+'-'+case['letter'], title=case['title'],
                category=section['name'], profiles=case['profiles'], acceptance=case['acceptance'],
                evidence_mode='本轮实板重新执行；同工况共享只读记录', verdict='未执行',
                excluded_scope=case.get('excluded_scope', ''), observed='', remaining=[])
            if key == 'NAV-i':
                row['evidence_mode'] = '本轮重新执行原生MPPI软件夹具＋按V2.1核查历史实板失败记录；不是本轮实板故障注入'
            try:
                actions[key]()
                row.update(verdict='通过', observed=describe(key))
            except FileNotFoundError as error:
                row.update(verdict='未完整执行', observed='缺少本轮必需证据：'+str(error), remaining=[str(error)])
            except Exception as error:
                row.update(verdict='不通过', observed=str(error), remaining=[str(error)])
            row['evidence'] = sorted(set(current_inputs))
            report['records'].append(row)
    report['counts'] = dict(Counter(r['verdict'] for r in report['records']))
    report['case_count'] = len(report['records'])
    report['passed'] = report['case_count'] == (11 if args.phase == 'a' else 30) and all(r['verdict'] == '通过' for r in report['records'])
    raw(requirements_path)
    report['requirements_sha256'] = hashlib.sha256(requirements_path.read_bytes()).hexdigest()
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps(dict(passed=report['passed'], phase=args.phase, counts=report['counts'],
        incomplete={r['id']:r['observed'] for r in report['records'] if r['verdict'] != '通过'}), ensure_ascii=False, indent=2))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
