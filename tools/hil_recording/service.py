"""Attach passive recording to an already requested HIL run; never start or stop HIL."""
import argparse
import datetime as dt
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import time
import uuid

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
SCRIPTS = ROOT / 'docs/deliverables/20261116_hil_retest'
STORE = ROOT / '.local/hil/recordings'
PROFILES = ('ackermann_apple', 'tracked_sprayer', 'tracked_shredder',
            'tractor_powershift', 'tractor_cvt')


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


def save(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.' + str(os.getpid()) + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    temporary.replace(path)


def now():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def refresh_campaign(identity):
    if __package__:
        from .audit import update_campaign
    else:
        from audit import update_campaign
    return update_campaign(identity)


def run_path(run):
    if not re.fullmatch(r'[A-Za-z0-9_]{1,80}', run):
        raise ValueError('Invalid HIL run identity')
    return STORE / 'runs' / run


def current_state():
    return read(ROOT / '.local/hil/state.json')


def desktop_link():
    # A link exposes records without making another copy of raw logs or models.
    try:
        desktop = subprocess.check_output(['xdg-user-dir', 'DESKTOP'], text=True, timeout=3).strip()
        path = Path(desktop) / '控制器HIL测试记录'
        if Path(desktop).is_dir() and not path.exists() and not path.is_symlink():
            path.symlink_to(STORE / 'campaigns', target_is_directory=True)
    except (OSError, subprocess.SubprocessError):
        pass


def attach(run, profile):
    target = run_path(run)
    state = current_state()
    if profile not in PROFILES or state.get('run_id') != run or state.get('profile') != profile:
        raise ValueError('Recording must match the actual requested HIL session')
    STORE.mkdir(parents=True, exist_ok=True)
    with (STORE / 'index.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        if target.exists():
            request = read(target / 'request.json')
            if request['profile'] != profile:
                raise ValueError('Existing recording belongs to a different profile')
            return read(target / 'status.json')
        pointer = STORE / 'current.json'
        previous = read(pointer) if pointer.exists() else {}
        day = dt.datetime.now().date().isoformat()
        if previous.get('day') != day or previous.get('closed') or not previous:
            identity = dt.datetime.now().strftime('%Y%m%d_%H%M%S_') + uuid.uuid4().hex[:6]
            previous = dict(campaign=identity, day=day, started_at=now(), profiles={}, closed=False)
        target.mkdir(parents=True, exist_ok=False)
        request = dict(run_id=run, profile=profile, campaign=previous['campaign'], requested_at=now(),
                       requirements_version='V2.2', source='Unity existing HIL start button',
                       requirements_sha256=hashlib.sha256((HERE / 'requirements.json').read_bytes()).hexdigest(),
                       hardware_commands_from_recorder=False)
        save(target / 'request.json', request)
        value = dict(**request, phase='queued', message='自动记录已排队', updated_at=now())
        save(target / 'status.json', value)
        previous['profiles'].setdefault(profile, []).append(run)
        save(pointer, previous)
        campaign = STORE / 'campaigns' / previous['campaign']
        save(campaign / 'index.json', previous)
        unit = 'agribot-hil-record-' + run
        command = ['systemd-run', '--user', '--collect', '--property=Type=exec', '--unit=' + unit,
                   '--property=WorkingDirectory=' + str(ROOT),
                   '--setenv=AGRIBOT_HIL_CONFIG=' + os.environ.get('AGRIBOT_HIL_CONFIG', str(ROOT / '.local/hil/connection.json')),
                   '/usr/bin/python3', str(Path(__file__).resolve()), 'worker', '--run', run]
        try:
            result = subprocess.run(command, capture_output=True, text=True, timeout=10)
            if result.returncode:
                raise RuntimeError(result.stderr.strip())
        except (OSError, subprocess.SubprocessError, RuntimeError) as error:
            value.update(phase='failed', message='自动记录未启动：' + str(error), updated_at=now())
            save(target / 'status.json', value)
            raise
    desktop_link()
    return value


def snapshot_runtime(selected, target):
    source = ROOT / '.local/hil/runtime' / selected['runtime']
    folder = target / 'configuration'
    folder.mkdir(exist_ok=True)
    paths = [source / name for name in ('vehicle_config.json', 'provenance.json', 'vehicle.urdf', 'vehicle.sdf')]
    paths += [source / 'rdk' / name for name in ('mission.json', 'nav2.yaml', 'map.yaml')]
    records = {}
    for path in paths:
        if path.is_file():
            name = str(path.relative_to(source))
            destination = folder / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, destination)
            records[name] = hashlib.sha256(destination.read_bytes()).hexdigest()
    map_file = source / 'rdk/map.pgm'
    if map_file.is_file():
        records['rdk/map.pgm'] = hashlib.sha256(map_file.read_bytes()).hexdigest()
    save(folder / 'manifest.json', dict(profile=selected['id'], sha256=records,
         note='Map hash only; the recording does not duplicate large maps or models.'))


def worker(run):
    target = run_path(run)
    request = read(target / 'request.json')
    with (target / 'worker.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        try:
            _worker(run, target, request)
        except BaseException as error:
            save(target / 'status.json', dict(request, phase='failed', message='自动记录失败：' + str(error), updated_at=now()))
            refresh_campaign(request['campaign'])
            raise


def _worker(run, target, request):
    sys.path.insert(0, str(ROOT / 'tools/hil'))
    from agent import Agent
    from catalog import profile
    from qualify import inspect_run
    selected = profile(request['profile'])
    processes, handles, errors = {}, [], []
    status = dict(request)
    agent = Agent(ROOT / '.local/hil', 'pc')

    def update(phase, message):
        status.update(phase=phase, message=message, errors=errors, updated_at=now())
        save(target / 'status.json', status)

    def spawn(name, args, ros=False):
        handle = (target / (name + '-console.log')).open('x')
        handles.append(handle)
        command = ['/usr/bin/python3', *map(str, args)]
        if ros:
            command = agent.ros_command(selected, command)
        child = subprocess.Popen(command, env=agent.environment(selected), stdout=handle,
                                 stderr=subprocess.STDOUT, start_new_session=True)
        processes[name] = child
        return child

    def joined(name, timeout):
        code = processes[name].wait(timeout=timeout)
        if code:
            errors.append(name + '记录/核对失败，退出码' + str(code))
        return code

    complete = False
    try:
        update('recording', '正在记录本工况；不改变仿真或控制命令')
        spawn('observer', [SCRIPTS / 'observe_run.py', '--root', ROOT, '--run', run,
              '--profile', selected['id'], '--output', target / 'independent'])
        spawn('stamped-poses', [SCRIPTS / 'pose_audit.py', '--car', selected['car'],
              '--output', target / 'stamped-poses.jsonl'], ros=True)
        snapshot = attested = sensors = False
        deadline = time.monotonic() + 2700
        while time.monotonic() < deadline:
            state = current_state()
            if state.get('run_id') != run:
                raise RuntimeError('本工况未完成记录前已切换会话，未用下一工况补齐结果')
            phase = state.get('phase')
            if not snapshot and phase in ('starting_pc', 'starting_x5', 'waiting_ready', 'starting_mission', 'running', 'completed'):
                snapshot_runtime(selected, target)
                snapshot = True
            if phase == 'running' and not sensors:
                args = [SCRIPTS / 'sensor_audit.py', '--output', target / 'sensor-audit.json', '--seconds', '15']
                if selected['car'] == 'Car4':
                    args.append('--no-camera')
                spawn('sensors', args, ros=True)
                if selected['car'] == 'Car4':
                    spawn('harrow-history', [SCRIPTS / 'harrow_history.py', '--state', ROOT / '.local/hil/state.json',
                          '--run', run, '--output', target / 'harrow-history'], ros=True)
                sensors = True
            if phase == 'running' and not attested:
                spawn('runtime-attestation', [SCRIPTS / 'runtime_attestation.py', '--root', ROOT,
                      '--run', run, '--output', target / 'runtime-attestation.json'])
                attested = True
            if phase == 'completed':
                manifest = read(target / 'configuration/rdk/mission.json')
                goal = ([manifest['goals'][-1]['x'], manifest['goals'][-1]['y']]
                        if selected['car'] == 'Car4' else manifest['finish'][:2])
                update('terminal_window', '任务已到点，正在记录终点后5秒仿真窗口；请稍后再停止仿真')
                spawn('terminal', [SCRIPTS / 'terminal_hold.py', '--root', ROOT, '--run', run,
                      '--car', selected['car'], '--goal', *map(str, goal), '--output', target / 'terminal.json'], ros=True)
                if selected['car'] != 'Car0':
                    spawn('end-work', [HERE / 'end_work.py', '--root', ROOT, '--run', run,
                          '--car', selected['car'], '--output', target / 'end-work.json'], ros=True)
                joined('terminal', 100)
                if 'end-work' in processes:
                    joined('end-work', 100)
                complete = True
                break
            if phase in ('failed', 'stopped'):
                raise RuntimeError('任务未正常完成：' + state.get('message', phase))
            time.sleep(.25)
        else:
            raise TimeoutError('自动记录达到45分钟期限；未停止或改变HIL任务')
        for name in ('sensors', 'observer', 'runtime-attestation', 'harrow-history'):
            if name in processes:
                joined(name, 65)
        observer_manifest = read(target / 'independent/recorder-manifest.json')
        if observer_manifest.get('recorder_returncode') != 0:
            errors.append('独立只读记录器未正常退出')
        if not sensors or not attested:
            errors.append('未取得运行阶段传感器或已加载程序记录')
    except BaseException as error:
        errors.append(str(error))
    finally:
        # Only our passive subscribers are signalled, never HIL, ROS controllers or a flash writer.
        for child in processes.values():
            if child.poll() is None:
                os.killpg(child.pid, signal.SIGINT)
                try:
                    child.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    os.killpg(child.pid, signal.SIGTERM)
                    child.wait(timeout=10)
        for handle in handles:
            handle.close()
    update('waiting_stop', '观察已完成，请停止仿真；完整CAN日志将在停止后自动归档')
    # The physical CDC owner's log is committed only on normal shutdown. Do not
    # stop it here or archive a live/incomplete USB report as a closed capture.
    closed = False
    deadline = time.monotonic() + 86400
    while time.monotonic() < deadline:
        state = current_state()
        if state.get('run_id') == run:
            closed = state.get('phase') in ('stopped', 'failed') and not state.get('session_alive')
        else:
            closed = state.get('phase') in ('checking_protected_runtime', 'flashing', 'configuring_can',
                'starting_pc', 'starting_x5', 'waiting_ready', 'starting_mission', 'running', 'completed', 'stopped')
        if closed:
            break
        time.sleep(.5)
    if not closed:
        errors.append('未确认用户已正常停止工况，不能把运行中日志作为完整归档')
    update('archiving', '正在保存本工况原始记录并生成用例结果')
    try:
        task = inspect_run(selected, run) if complete and closed else {}
        save(target / 'mission-checks.json', dict(profile=selected['id'], run_id=run,
             passed=bool(complete and task.get('passed')), mission=task,
             scope='Existing Unity normal mission, not the V2.1 special-injection procedure'))
        post = spawn('postcheck', [SCRIPTS / 'postcheck.py', '--root', ROOT, '--directory', target])
        joined('postcheck', 380)
        if selected['id'] == 'ackermann_apple' and complete:
            spawn('apple-review', [SCRIPTS / 'review_apple.py', '--directory', target], ros=True)
            joined('apple-review', 120)
    except BaseException as error:
        errors.append('归档未完整：' + str(error))
    finally:
        for child in processes.values():
            if child.poll() is None:
                os.killpg(child.pid, signal.SIGTERM)
                child.wait(timeout=15)
        for handle in handles:
            if not handle.closed:
                handle.close()
    save(target / 'capture.json', dict(run_id=run, profile=selected['id'], completed=complete, closed=closed,
         captured_at=now(), errors=errors, commands_sent=False,
         note='App-owned logs collected after operator shutdown; the recorder never stops the HIL session.'))
    files = {str(p.relative_to(target)): hashlib.sha256(p.read_bytes()).hexdigest()
             for p in target.rglob('*') if p.is_file() and p.name not in ('status.json', 'worker.lock', 'evidence.json')}
    save(target / 'evidence.json', dict(run_id=run, sealed_at=now(), files=files))
    update('finished' if complete and not errors else 'failed',
           '自动记录已保存，可以停止仿真或切换下一工况' if complete and not errors else '记录存在缺项，请查看本工况记录')
    refresh_campaign(request['campaign'])


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('attach', 'worker', 'status'))
    parser.add_argument('--run', required=True)
    parser.add_argument('--profile', choices=PROFILES)
    args = parser.parse_args(argv)
    if args.action == 'attach':
        print(json.dumps(attach(args.run, args.profile), ensure_ascii=False))
    elif args.action == 'worker':
        worker(args.run)
    else:
        path = run_path(args.run) / 'status.json'
        print(json.dumps(read(path) if path.exists() else dict(phase='missing', message='本轮没有自动记录'), ensure_ascii=False))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
