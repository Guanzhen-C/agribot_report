"""Fast Unity-facing HIL control; the actual startup runs in its own user unit."""
import argparse
import datetime
import fcntl
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import traceback
import uuid

from backend import RuntimeBackend
from catalog import ROOT, firmware, profile
from config import load
from workflow import Startup

STATE = ROOT/'.local/hil'
UNIT = 'agribot-app-hil-session.service'
STOP_UNIT = 'agribot-app-hil-stop.service'
TERMINAL = ('stopped', 'completed', 'failed')
MESSAGES = {
    'queued': '正在准备HIL启动', 'checking': '正在检查硬件连接和场景',
    'stopping_previous': '正在停止上一轮仿真', 'checking_protected_runtime': '正在核对X5真车代码保护',
    'flashing': '正在烧录并校验6750固件', 'configuring_can': '正在配置HIL通信',
    'starting_pc': '正在启动Gazebo场景', 'starting_x5': '正在启动X5算法',
    'waiting_ready': '正在等待定位和硬件反馈就绪', 'running': 'HIL任务正在运行',
    'stopping_after_error': '正在停止HIL并撤销授权', 'completed': 'HIL任务已完成',
    'stopped': 'HIL仿真已停止', 'failed': 'HIL启动或运行失败', 'stopping': '正在停止HIL仿真'}


def unit_active(name=UNIT):
    result = subprocess.run(['systemctl', '--user', 'is-active', name], capture_output=True, text=True)
    return result.stdout.strip() in ('active', 'activating', 'deactivating')


def read_status():
    path = STATE/'state.json'
    if not path.exists(): return {'phase': 'stopped', 'session_alive': False}
    return json.loads(path.read_text())


def save_status(value):
    STATE.mkdir(parents=True, exist_ok=True)
    temporary = STATE/('state-'+str(os.getpid())+'.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n')
    temporary.replace(STATE/'state.json')


def queue_start(identifier, algorithm):
    selected = profile(identifier)
    if selected is None: raise ValueError('该车辆、农具和场景尚未开放HIL。')
    if algorithm and algorithm != selected['algorithm']:
        raise ValueError('首期HIL使用该组合已验证的默认算法；普通仿真的算法选择不受影响。')
    firmware(selected['firmware'])
    load()
    STATE.mkdir(parents=True, exist_ok=True)
    with (STATE/'request.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        if unit_active() or unit_active(STOP_UNIT):
            raise RuntimeError('HIL任务仍在启动、运行或清理，请等待停止完成。')
        run_id = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S')+'_'+uuid.uuid4().hex[:8]
        stop = STATE/'stop.request'
        if stop.exists(): stop.unlink()
        save_status({'phase': 'queued', 'profile': identifier, 'run_id': run_id,
            'message': MESSAGES['queued'], 'updated_at': time.time(), 'session_alive': False})
        result = subprocess.run(['systemd-run', '--user', '--collect', '--property=Type=exec', '--unit='+UNIT,
            '--setenv=DISPLAY='+os.environ.get('DISPLAY', ':0'),
            '--setenv=XAUTHORITY='+os.environ.get('XAUTHORITY', '/run/user/1000/gdm/Xauthority'),
            '--setenv=AGRIBOT_HIL_CONFIG='+os.environ.get('AGRIBOT_HIL_CONFIG', str(STATE/'connection.json')),
            '--property=WorkingDirectory='+str(ROOT),
            '/usr/bin/python3', str(Path(__file__).resolve()), 'worker', '--profile', identifier, '--run-id', run_id],
            capture_output=True, text=True)
        if result.returncode:
            save_status({'phase': 'failed', 'profile': identifier, 'run_id': run_id,
                'message': result.stderr.strip(), 'session_alive': False, 'updated_at': time.time()})
            raise RuntimeError(result.stderr.strip())
    return read_status()


def stop():
    STATE.mkdir(parents=True, exist_ok=True)
    with (STATE/'request.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        (STATE/'stop.request').touch()
        if not unit_active() and not unit_active(STOP_UNIT):
            result = subprocess.run(['systemd-run', '--user', '--collect', '--property=Type=exec',
                '--unit='+STOP_UNIT,
                '--setenv=AGRIBOT_HIL_CONFIG='+os.environ.get('AGRIBOT_HIL_CONFIG', str(STATE/'connection.json')),
                '/usr/bin/python3', str(Path(__file__).resolve()), 'worker', '--stop-only'],
                capture_output=True, text=True)
            if result.returncode: raise RuntimeError(result.stderr.strip())
    return {'requested': 'stop', 'message': '停止请求已提交；若正在烧录，将在校验结束后停止。'}


def worker(identifier, run_id, stop_only=False):
    previous = read_status()
    run_id = run_id or previous.get('run_id') or ('stop_'+uuid.uuid4().hex)
    identifier = identifier or previous.get('profile', '')
    run = STATE/'runs'/run_id
    run.mkdir(parents=True, exist_ok=True)
    current = dict(previous, profile=identifier, run_id=run_id)
    def emit(phase, detail=None):
        current.update(phase=phase, message=detail or MESSAGES.get(phase, phase), updated_at=time.time())
        if phase in ('starting_x5', 'waiting_ready', 'running', 'completed'):
            current['session_alive'] = True
        if phase == 'stopped': current['session_alive'] = False
        save_status(current)
        with (run/'stages.jsonl').open('a') as stream:
            stream.write(json.dumps(current, ensure_ascii=False)+'\n')
    backend = None
    try:
        backend = RuntimeBackend(load(), run, emit, lambda: (STATE/'stop.request').exists())
        if stop_only:
            emit('stopping')
            backend.safe_stop()
            emit('stopped')
            return
        selected = Startup(backend, emit, backend.cancelled).run(identifier)
        phase = backend.monitor(selected)
        emit(phase)
    except BaseException as error:
        (run/'error.log').write_text(traceback.format_exc())
        if backend is not None:
            try: backend.safe_stop(); current['session_alive'] = False
            except Exception as failure:
                (run/'cleanup-error.log').write_text(str(failure))
        if (isinstance(error, InterruptedError) or (backend and backend.cancelled())) and not current.get('session_alive'):
            emit('stopped')
        else:
            emit('failed', str(error))
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('start', 'stop', 'status', 'worker'))
    parser.add_argument('--profile')
    parser.add_argument('--algorithm')
    parser.add_argument('--run-id')
    parser.add_argument('--stop-only', action='store_true')
    args = parser.parse_args()
    if args.action == 'worker': worker(args.profile, args.run_id, args.stop_only); return
    if args.action == 'start': value = queue_start(args.profile, args.algorithm)
    elif args.action == 'stop': value = stop()
    else: value = read_status()
    print(json.dumps(value, ensure_ascii=False))


if __name__ == '__main__':
    try: main()
    except Exception as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
