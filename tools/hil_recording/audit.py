"""Offline V2.2 normal-workflow verdicts; never issue ROS/CAN/control commands."""
import csv
import fcntl
import hashlib
import json
import math
from functools import cached_property
from pathlib import Path

try:
    from .service import HERE, ROOT, STORE, PROFILES, read, save, now
except ImportError:
    from service import HERE, ROOT, STORE, PROFILES, read, save, now


def require(value, message):
    if not value:
        raise ValueError(message)


class Evidence:
    def __init__(self, folder, identifier, run):
        self.folder, self.identifier, self.run = Path(folder), identifier, run
        self.inputs, self.cache = {}, {}
        self.sealed = None

    def raw(self, relative):
        path = (self.folder / relative).resolve()
        require(path.is_relative_to(self.folder.resolve()), '证据路径越界')
        data = path.read_bytes()
        digest = hashlib.sha256(data).hexdigest()
        if self.sealed is not None:
            require(relative in self.sealed and self.sealed[relative] == digest, '证据未封存或发生变化：' + relative)
        self.inputs[relative] = digest
        return data

    def json(self, relative):
        return json.loads(self.raw(relative))

    def lines(self, relative):
        return [json.loads(line) for line in self.raw(relative).splitlines() if line.strip()]

    def verify(self):
        request = self.json('request.json')
        require(request['run_id'] == self.run and request['profile'] == self.identifier, '请求与实际工况编号不符')
        require(request['requirements_version'] == 'V2.2', '不是V2.2记录')
        require(request['requirements_sha256'] == hashlib.sha256((HERE / 'requirements.json').read_bytes()).hexdigest(), '判据版本或内容已变化')
        seal = self.json('evidence.json')
        require(seal['run_id'] == self.run and seal['files'], '没有本次完整证据清单')
        require('request.json' in seal['files'], '封存清单缺少本次请求')
        for name, digest in seal['files'].items():
            require(hashlib.sha256(self.raw(name)).hexdigest() == digest, '原始证据发生变化：' + name)
        self.sealed = seal['files']
        manifest = self.json('hardware-evidence/manifest.json')
        require(manifest['run_id'] == self.run and manifest['files'], '物理记录不属于本次运行')
        for name, row in manifest['files'].items():
            require(hashlib.sha256(self.raw('hardware-evidence/' + name)).hexdigest() == row['sha256'], '物理原始日志变化：' + name)

    @cached_property
    def selected(self):
        profiles = read(ROOT / 'unity/Assets/AgribotConfig/Resources/hil_profiles.json')['profiles']
        return next(p for p in profiles if p['id'] == self.identifier)

    @cached_property
    def task(self):
        result = self.json('hardware-evidence/pc/mission-result.json')
        require(result['status'] == 'completed', '任务不是正常完成')
        return result['result']

    @cached_property
    def observer(self):
        name = 'independent-pc.json' if self.identifier == 'ackermann_apple' else 'independent.json'
        return self.json('independent/' + name)

    def samples(self, key):
        data = self.observer
        if self.identifier == 'ackermann_apple':
            if key == 'truth':
                return [r[1:4] + r[4:8] for r in data['traces']['/hil/ground_truth']]
        return [row[2] for row in data['samples'][key]]

    def check(self, name):
        if name not in self.cache:
            before = set(self.inputs)
            text = getattr(self, 'check_' + name)()
            self.cache[name] = dict(passed=True, observed=text, evidence=sorted(set(self.inputs) - before))
        return self.cache[name]

    def check_mission(self):
        task = self.task
        require(task.get('passed') is True or task.get('status') in ('succeeded', 'completed'), '缺少实际任务成功结果')
        return '本次X5任务正常成功；运行编号：' + self.run

    def check_firmware(self):
        catalog = read(ROOT / 'unity/Assets/AgribotConfig/Resources/hil_profiles.json')
        image = next(f for f in catalog['firmwares'] if f['id'] == self.selected['firmware'])
        actual = self.json('hardware-evidence/pc/firmware.json')
        require(all(actual[k] == image[k] for k in ('id', 'sha256', 'bitrate')), '实际固件、SHA或速率不匹配')
        return f"实际固件{actual['id']}；CAN {actual['bitrate']} bit/s；SHA {actual['sha256']}"

    def check_flash(self):
        data = self.raw('hardware-evidence/pc/flash.log').decode()
        require('Programming Finished' in data and 'Verified OK' in data, '本次烧录未确认编程和校验完成')
        return '本次日志包含Programming Finished、Verified OK。'

    def check_sequence(self):
        phases = [row['phase'] for row in self.lines('hardware-evidence/pc/stages.jsonl')]
        wanted = ['waiting_ready', 'starting_mission', 'running', 'completed']
        indices = [phases.index(name) for name in wanted]
        require(indices == sorted(indices) and len(set(indices)) == 4, '就绪、提交、运行、完成顺序不符')
        return '实际阶段顺序：' + ' → '.join(wanted)

    def check_can(self):
        data = self.observer
        states = (data['can'] if self.identifier == 'ackermann_apple' else
                  [r[2] for r in data['samples']['can_status' if self.identifier.startswith('tractor_') else 'status']])
        require(len(states) >= 3, '实际CAN状态样本不足')
        require(not any(s.get('fault') or s.get('errors', 0) or s.get('transport_recoveries', 0) for s in states), '正常工况CAN状态存在错误或恢复事件')
        require(not data.get('transport_events'), '记录到正常工况通信异常')
        if self.identifier.startswith('tractor_'):
            self.check('power_feedback')
        else:
            rx, tx = ('rx_commands', 'tx_feedback') if self.identifier == 'ackermann_apple' else ('rx', 'tx')
            require(any(s.get(rx, 0) > 0 and s.get(tx, 0) > 0 for s in states), '无实际CAN双向反馈计数')
        return f'独立PC实际CAN状态{len(states)}组；无记录的fault/errors/transport_recoveries。'

    def power(self):
        data = self.json('powertrain-audit.json')
        require(data.get('audit_passed') is True, '正常动力报文审计未通过')
        return data

    def check_powershift(self):
        data = self.power()
        gears = [g for g in data['actual_gear_values'] if g > 0]
        require(2 in data['active_modes'] and len(set(gears)) >= 2, '未记录实际动力换挡')
        self.check('mission')
        return '实际动力换挡挡位：' + str(gears)

    def check_cvt(self):
        data = self.power()
        low, high = data['cvt_actual_ratio_range']
        require(3 in data['active_modes'] and 200 - .001 <= low < high <= 900 + .001, 'CVT实际速比没有变化或越出原范围')
        self.check('mission')
        return f'实际总传动比范围：{low:.3f}至{high:.3f}。'

    def check_power_feedback(self):
        data = self.power()
        rows = data['feedback_payload_matching']
        require(set(rows) == {'642', '643', '644', '645'} and
                all(r['received'] > 0 and r['matched'] == r['received'] for r in rows.values()), '四类实际动力反馈缺失或负载不一致')
        return '四类反馈实际收到/匹配：' + str(rows)

    def check_tractor_route(self):
        data = self.task
        stages, goals = data['stages'], data['manifest']['goals']
        require(len(stages) == len(goals) == 5 and [s['name'] for s in stages] == [g['name'] for g in goals], '五阶段数量或顺序不符')
        require(all(s['action'] == 'completed' and s['planned_points'] > 1 for s in stages), '存在未完成或无路径阶段')
        return '实际五阶段：' + ' → '.join(s['name'] for s in stages)

    def check_end_work(self):
        data = self.json('end-work.json')
        require(data['run_id'] == self.run and data['passed'] is True, '本次终点机具静止/制动观察未通过')
        require(data['samples'] >= 3 and len(data['final_samples']) >= 3, '终点最后1秒实际机具样本不足')
        return f"终点后观察{data['observation_sim_seconds']:.3f}秒仿真时间；最后1秒{len(data['final_samples'])}组实际状态满足。"

    def check_protected(self):
        rows = self.lines('hardware-evidence/pc/real_runtime_checks.jsonl')
        require(len(rows) >= 3 and all(r['unchanged'] is True for r in rows), '真车文件保护检查缺失或发生变化')
        return f'本次{len(rows)}道实际真车保护检查均unchanged=true。'

    def check_binding(self):
        self.check('firmware')
        self.check('runtime')
        self.json('configuration/rdk/mission.json')
        return f"本次运行{self.run}对应{self.selected['car']}＋{self.selected['implement']}／{self.selected['scene']}。"

    def apple(self):
        data = self.json('apple-independent-audit.json')
        require(data['passed'] is True, '独立采摘或夹爪保持核对未通过')
        return data

    def check_apple_pick(self):
        data = self.apple()
        fruit = self.task['harvested']
        require(data['physical_picking']['status'] == 'passed' and len(set(fruit)) >= 2, '未完成两颗不同苹果的实际采摘')
        return '实际采摘不同苹果：' + ', '.join(fruit)

    def check_apple_hold(self):
        data = self.apple()['transit_gripper']
        require(data['status'] == 'passed', '行驶夹爪保持核对未通过')
        return '行驶保持实际审计通过；原上限(m)：' + str(data['maximum_allowed_error_m'])

    def check_apple_cargo(self):
        data = self.task
        fruit, cargo = set(data['harvested']), data['final_cargo']
        require(len(fruit) >= 2 and {r['model'] for r in cargo} == fruit and all(r['inside_tray'] for r in cargo), '采摘结束果盒留果不完整')
        return f'本次终点果盒内保留{len(fruit)}颗不同苹果。'

    def check_sprayer(self):
        data = self.task
        require(data['actual_work_joint_names'] == ['work_fan_joint', 'work_pump_joint'], '实际作业轴与植保机不符')
        require(any(r['active'] is True for r in data['work_events']) and
                any(len(row) == 3 and min(row[1:]) > 50 for row in data['actual_work']), '没有风机水泵实际联动样本')
        self.check('mission')
        return '存在实际风机/水泵同时大于50 rad/s的作业样本；截图另记录喷雾外观。'

    def check_shredder(self):
        data = self.task
        require(data['actual_work_joint_names'] == ['work_shredder_joint'] and
                any(r['active'] is True for r in data['work_events']) and
                any(len(row) == 2 and row[1] > 50 for row in data['actual_work']), '没有实际碎枝轴工作样本')
        self.check('end_work')
        self.check('mission')
        return '实际碎枝轴转动超过50 rad/s，任务成功，终点实际停转观察通过。'

    def check_hitch(self):
        states = self.samples('hitch')
        require(any(r[0] < 150 for r in states) and any(r[0] > 8850 for r in states), '没有实际下放/抬升位置覆盖')
        require(any(r[2] == 1 for r in states), '没有实际入土作业状态')
        self.check('tractor_route')
        return f'实际悬挂状态{len(states)}组，覆盖低于150/高于8850计数及入土作业。'

    def check_harrow(self):
        data = self.json('harrow-history/result.json')
        require(data['passed'] is True and data['maximum_segments'] > 0 and data['raised_history_preserved_sim_seconds'] >= 1., '作业段增长或抬升后历史保留未通过')
        return f"实际作业段最多{data['maximum_segments']}段，抬升历史保留{data['raised_history_preserved_sim_seconds']:.3f}秒仿真时间。"

    def check_sensors(self):
        data = self.json('sensor-audit.json')
        require(data['passed'] is True and all(k in data['topics'] for k in ('clock', 'imu', 'gnss')), '运行时传感器/时钟审计未通过')
        return '实际运行阶段传感器计数：' + str({k: v['count'] for k, v in data['topics'].items()})

    def check_runtime(self):
        data = self.json('runtime-attestation.json')
        require(data['passed'] is True and data['current']['run_id'] == self.run and data['current']['profile'] == self.identifier, '已加载程序证明与本次运行不符')
        return '已加载独立HIL原生MPPI控制库和实际部署模块证明通过；定位方案：' + self.selected['algorithm']

    def check_configuration(self):
        data = self.json('configuration/manifest.json')
        entries = data['sha256']
        require(data['profile'] == self.identifier and all(k in entries for k in ('rdk/mission.json', 'rdk/nav2.yaml', 'rdk/map.pgm')), '任务、导航参数或地图哈希缺失')
        require(any(k in entries for k in ('vehicle.urdf', 'vehicle.sdf')), '缺少本次车型模型记录')
        for name, digest in entries.items():
            if name != 'rdk/map.pgm':
                require(hashlib.sha256(self.raw('configuration/' + name)).hexdigest() == digest, '保存配置与清单不符：' + name)
        return '本次地图像素SHA：' + entries['rdk/map.pgm'] + '；配置项：' + str(len(entries))

    def check_planning(self):
        self.check('runtime')
        if self.identifier.startswith('tractor_'):
            return self.check('tractor_route')['observed']
        rows = self.task['navigation']
        require(rows and all(len(r.get('path', [])) > 1 for r in rows), '没有本次非空规划路径')
        require(all(r.get('executed') is True or r.get('status') == 'succeeded' for r in rows), '保存路径没有实际执行记录')
        return '本次实际保存导航规划段：' + str(len(rows))

    def check_movement(self):
        if self.identifier == 'ackermann_apple':
            rows = self.samples('truth')
            positions, quaternions = [r[:2] for r in rows], [r[3:7] for r in rows]
        else:
            rows = self.samples('truth')
            positions, quaternions = [r['position'][:2] for r in rows], [r['quaternion'] for r in rows]
        require(len(rows) >= 3, '实际位姿样本不足')
        extent = math.hypot(*(max(p[i] for p in positions) - min(p[i] for p in positions) for i in (0, 1)))
        yaw = [math.atan2(2*(q[3]*q[2]+q[0]*q[1]), 1-2*(q[1]*q[1]+q[2]*q[2])) for q in quaternions]
        unwrapped = [yaw[0]]
        for a, b in zip(yaw, yaw[1:]):
            unwrapped.append(unwrapped[-1] + math.atan2(math.sin(b-a), math.cos(b-a)))
        turn = max(unwrapped) - min(unwrapped)
        require(extent > 2 and turn > .2, '未实际展示规定的平移与转向')
        return f'实际位姿{len(rows)}组；平移范围{extent:.3f}m，航向变化{turn:.3f}rad。'

    def check_poses(self):
        data = self.json('pose-audit.json')
        for key in ('localization_xy', 'path_tracking_xy'):
            row = data[key]
            require(row['samples'] > 0 and all(isinstance(row[k], (int, float)) and math.isfinite(row[k]) for k in ('p95_m', 'maximum_m')), '缺少有效的实际定位或跟踪测量')
        return '按仿真时戳测量：' + str({k: data[k] for k in ('localization_xy', 'path_tracking_xy')})

    def check_route(self):
        if self.identifier.startswith('tractor_'):
            return self.check('tractor_route')['observed']
        data = self.task
        self.check('mission')
        if self.identifier == 'ackermann_apple':
            require(len(data['navigation']) >= 2 and all(r['status'] == 'succeeded' for r in data['navigation']), '采摘前后导航阶段未完成')
            return '实际采摘前后导航阶段均成功。'
        events = data['route_passage_events']
        points = data['manifest']['fixed_route_points']
        require(data['navigation'][0]['requested_goals'] == points, '本次请求未按默认三点顺序提交')
        index = 0
        for event in events:
            require(event['source'] == 'native_nav2_remaining_path_feedback' and event['from_index'] == index
                    and index < event['to_index'] < len(points), '原生路径推进记录不连续或顺序错误')
            index = event['to_index']
        require(index == len(points)-1 and data['independent_endpoint_error_m'] <= data['manifest']['goal_tolerance_m'], '剩余路径推进或最终到点核查不完整')
        return '默认三点顺序已提交；原生剩余路径推进到末段，独立终点位置满足原容差。'

    def check_terminal(self):
        data = self.json('terminal.json')
        require(data['run_id'] == self.run and data['passed'] is True and data['simulation_seconds'] >= 5., '本次终点5秒位置/速度观察未通过')
        return f"终点位置误差{data['final_error_m']:.4f}m（半轴距{data['half_wheelbase_m']:.4f}m）；最大速度{data['maximum_planar_speed_mps']:.4f}m/s。"

    def check_archive(self):
        self.check('terminal')
        capture = self.json('capture.json')
        require(capture['run_id'] == self.run and capture['completed'] is True and capture['closed'] is True and not capture['errors'], '本次记录未完整完成、停止或存在采集错误')
        require(not self.json('terminal.json')['new_planner_goals'], '终点后新增规划')
        return '本次完整归档通过哈希核对；终点后没有新增规划。截图由测试人员另附。'


def evaluate_campaign(index, store=STORE):
    spec = read(HERE / 'requirements.json')
    readers, states = {}, {}
    for identifier, attempts in index['profiles'].items():
        run = attempts[-1]
        folder = store / 'runs' / run
        try:
            require(folder.resolve().is_relative_to((store / 'runs').resolve()), '记录目录越界')
            status = read(folder / 'status.json')
        except (OSError, ValueError, TypeError) as error:
            states[identifier], readers[identifier] = 'failed', '记录状态不可读：' + str(error)
            continue
        states[identifier] = status['phase']
        if status['phase'] not in ('finished', 'failed'):
            continue
        evidence = Evidence(folder, identifier, run)
        try:
            evidence.verify()
            readers[identifier] = evidence
        except (OSError, ValueError, KeyError, TypeError) as error:
            readers[identifier] = str(error)
    rows = []
    for number, case in enumerate(spec['cases'], 1):
        profiles = PROFILES if case['profiles'] == 'all' else case['profiles']
        results = []
        for identifier in profiles:
            result = dict(profile=identifier, verdict='未运行', observed='请按原流程运行此工况。')
            if identifier in states:
                result.update(run_id=index['profiles'][identifier][-1], verdict='记录中', observed='正在记录或等待正常停止归档。')
            if identifier in readers:
                evidence = readers[identifier]
                try:
                    require(isinstance(evidence, Evidence), str(evidence))
                    result.update(evidence.check(case['check']), verdict='通过')
                except (OSError, ValueError, KeyError, TypeError, IndexError) as error:
                    result.update(verdict='不通过', observed=str(error))
            results.append(result)
        verdict = ('不通过' if any(r['verdict'] == '不通过' for r in results) else
                   '通过' if all(r['verdict'] == '通过' for r in results) else
                   '记录中' if any(r['verdict'] == '记录中' for r in results) else '未运行')
        group = case['id'].split('-')[0]
        rows.append(dict(case, id='TC-HIL-' + group + '-001-' + case['id'][-1],
            alias=f'YL-FU-{number:03d}', category=spec['groups'][group], verdict=verdict, results=results))
    return dict(requirements_version='V2.2', campaign=index['campaign'], updated_at=now(),
        scope=spec['scope'], revision=spec['revision'],
        selected_runs={k: v[-1] for k, v in index['profiles'].items()},
        passed=all(r['verdict'] == '通过' for r in rows),
        counts={key: sum(r['verdict'] == key for r in rows) for key in ('通过', '不通过', '记录中', '未运行')},
        records=rows, screenshots='测试人员按工况截图并随报告附上；本程序不伪造或代签人工外观审查。')


def update_campaign(identity):
    folder = STORE / 'campaigns' / identity
    with (folder / 'audit.lock').open('a') as audit_lock:
        fcntl.flock(audit_lock, fcntl.LOCK_EX)
        # Hashing large closed CAN traces must not delay the next GUI start.
        while True:
            index = read(folder / 'index.json')
            report = evaluate_campaign(index)
            if read(folder / 'index.json')['profiles'] == index['profiles']:
                break
        save(folder / 'cases.json', report)
        with (folder / 'cases.csv').open('w', encoding='utf-8-sig', newline='') as stream:
            writer = csv.writer(stream)
            writer.writerow(['编号', '对应条款', '类别', '名称', '结论', '工况', '实际记录'])
            for row in report['records']:
                writer.writerow([row['alias'], row['id'], row['category'], row['title'], row['verdict'],
                    ', '.join(r['profile'] for r in row['results']),
                    '\n'.join(r['profile'] + '：' + r['verdict'] + '；' + r['observed'] for r in row['results'])])
        lines = ['控制器HIL五工况自动测试记录／V2.2', '本轮：' + identity,
                 '汇总：' + str(report['counts']), report['scope'], report['revision'],
                 '每类工况采用本轮最后一次尝试；之前失败目录仍保留，不回退挑选旧成功结果。', '']
        for row in report['records']:
            lines.extend([row['alias'] + '／' + row['id'] + ' ' + row['title'] + '：' + row['verdict'],
                          '判据：' + row['acceptance']])
            lines.extend(r['profile'] + '／' + r.get('run_id', '未运行') + '：' + r['verdict'] + '；' + r['observed'] for r in row['results'])
            lines.append('')
        (folder / '30项测试记录.txt').write_text('\n'.join(lines), encoding='utf-8')
        # A failed fifth profile must remain retryable within this round.
        closed = report['passed']
        with (STORE / 'index.lock').open('a') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX)
            latest = read(folder / 'index.json')
            if latest['profiles'] == index['profiles']:
                index['closed'] = closed
                save(folder / 'index.json', index)
                current = read(STORE / 'current.json')
                if current['campaign'] == identity:
                    save(STORE / 'current.json', index)
    return report
