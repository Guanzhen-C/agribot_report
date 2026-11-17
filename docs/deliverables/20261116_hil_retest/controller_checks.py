"""Run existing controller acceptance checks on the isolated physical HIL stack.

Not a navigation mission: only the existing bounded verification tools publish
high-level requests. The Gazebo actuator interfaces remain owned by the CAN bridge.
"""
import argparse
import datetime
import fcntl
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time
import uuid


def bridge_pids():
    result = []
    for proc in Path('/proc').glob('[0-9]*/cmdline'):
        try:
            argv = proc.read_bytes().split(b'\0')
        except (FileNotFoundError, PermissionError, ProcessLookupError):
            continue
        if any(arg.endswith(b'/agribot_hil_hitch/scripts/pc_bridge.py') for arg in argv) and b'--powertrain' in argv:
            result.append(int(proc.parent.name))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--profile', choices=('tractor_cvt', 'tractor_powershift', 'tracked_sprayer',
                        'tracked_shredder', 'ackermann_apple'), default='tractor_cvt')
    parser.add_argument('--checks',nargs='+',choices=('hitch','harrow','powertrain','parameters','sprayer','shredder','authority','arm','visual','watchdog','navigation','existing-spray'),
                        default=['hitch','powertrain'])
    args = parser.parse_args()
    if args.execute and set(args.checks) & {'visual', 'navigation'}:
        raise ValueError('当前测试范围禁止新增场景物体；已停用会插入测试障碍的旧专项。请使用现有条件版检查。')
    if not args.execute:
        print('No hardware actions without --execute')
        return
    root, output = args.root.resolve(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    sys.path.insert(0, str(root/'tools/hil'))
    from agent import Agent
    from backend import RuntimeBackend
    from catalog import profile, firmware
    from cli import save_status, read_status
    from config import load
    selected = profile(args.profile)
    supported = ({'hitch', 'harrow', 'powertrain', 'parameters'} if selected['car'] == 'Car4' else
                 ({'shredder', 'watchdog'} if args.profile == 'tracked_shredder' else {'sprayer', 'visual', 'watchdog', 'existing-spray'}) if selected['car'] == 'Car3' else
                 {'arm', 'watchdog'}) | {'authority', 'navigation'}
    if not set(args.checks) <= supported:
        raise ValueError('Checks do not match the installed vehicle/implement')
    stop_request = root/'.local/hil/stop.request'
    with (root/'.local/hil/request.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        previous = read_status()
        if previous.get('session_alive') or previous.get('phase') not in ('stopped','failed','completed'):
            raise RuntimeError('Previous HIL cleanup must be confirmed before controller checks')
        # Same transaction as the normal CLI: a cancelled prior session must
        # not cancel a newly requested test. New stop requests remain effective.
        stop_request.unlink(missing_ok=True)
    run_id = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S')+'_'+uuid.uuid4().hex[:8]
    run = root/'.local/hil/runs'/run_id
    report = dict(scope='New physical X5/6750/USB-CAN controller checks; not a full route',
                  passed=False, run_id=run_id, profile=selected['id'], phases=[], checks=[])
    agent = Agent(root/'.local/hil', 'pc')

    def persist():
        (output/'controller-checks.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')

    def emit(phase, detail=None, **fields):
        value = dict(at=time.time(), phase=phase, detail=detail, **fields)
        report['phases'].append(value)
        save_status(dict(value, run_id=run_id, profile=selected['id'], controller_acceptance_only=True,
                         message=detail or phase,session_alive=phase not in ('stopped','failed')))
        persist()
        print(json.dumps(value,ensure_ascii=False),flush=True)

    backend = RuntimeBackend(load(), run, emit, stop_request.exists)
    try:
        emit('checking')
        backend.preflight(selected, firmware(selected['firmware']))
        emit('stopping_previous')
        backend.stop_previous()
        emit('checking_protected_runtime')
        backend.verify_real_runtime()
        emit('flashing')
        backend.flash(firmware(selected['firmware']))
        if backend.cancelled():
            raise InterruptedError('Controller checks cancelled after firmware verification')
        emit('configuring_can')
        backend.configure_bus(firmware(selected['firmware'])['bitrate'])
        emit('starting_pc')
        backend.start_pc(selected)
        emit('starting_x5')
        backend.start_x5(selected)
        emit('waiting_ready')
        backend.wait_ready(selected)
        backend.verify_real_runtime()
        emit('running', 'Controller tests only; no navigation mission')
        with (output/'runtime-attestation-console.log').open('x') as log:
            result = subprocess.run(['/usr/bin/python3', str(root/
                'docs/deliverables/20261116_hil_retest/runtime_attestation.py'), '--root', str(root),
                '--run', run_id, '--output', str(output/'runtime-attestation.json')],
                stdout=log, stderr=subprocess.STDOUT, timeout=60.)
        report['checks'].append(dict(name='runtime-attestation', returncode=result.returncode))
        if result.returncode:
            raise RuntimeError('Actual native library or installed Python attestation failed')
        scripts = root/'ros2_ws/src/agribot_hil_hitch/scripts'
        kind = 'shredder' if selected['id'] == 'tracked_shredder' else 'sprayer'
        checks = [('authority', [str(root/'docs/deliverables/20261116_hil_retest/authority_checks.py'),
                                '--car', selected['car'], '--kind', kind,
                                '--output', str(output/'authority.json')]),
                  ('hitch', [str(scripts/'verify_physical_hil.py'), '--output', str(output/'hitch.json')]),
                  ('powertrain', [str(scripts/'verify_powertrain.py'), '--physical', '--output', str(output/'powertrain')])]
        checks.append(('parameters',[str(root/'docs/deliverables/20261116_hil_retest/parameter_checks.py'),
                                    '--root',str(root),'--output',str(output/'parameters')]))
        checks.append(('harrow', [str(root/'docs/deliverables/20261116_hil_retest/harrow_visual_checks.py'),
            '--root', str(root), '--transmission', selected.get('transmission', 'powershift_auto'),
            '--output', str(output/'harrow-visual')]))
        if selected['car'] == 'Car4':
            pids = bridge_pids()
            if len(pids) != 1:
                raise RuntimeError('Exactly one dedicated native bridge is required for a bounded feedback interruption')
            checks[2][1].extend(['--native-bridge-pid', str(pids[0])])
        elif selected['car'] == 'Car3':
            apple = root/'ros2_ws/src/agribot_hil_apple/scripts'
            for kind, script in [('sprayer', 'verify_tracked_work_hil.py'), ('shredder', 'verify_shredder_hil.py')]:
                checks.append((kind, [str(apple/script), '--output', str(output/(kind+'.json')),
                                      '--drive-topic', '/hil/navigation/cmd_vel']))
            checks.append(('visual', [str(root/'docs/deliverables/20261116_hil_retest/spray_visual_checks.py'),
                                      '--output', str(output/'spray-visual')]))
            checks.append(('existing-spray', [str(root/
                'docs/deliverables/20261116_hil_existing_conditions/spray_existing.py'),
                '--output', str(output/'spray-existing')]))
        elif selected['car'] == 'Car0':
            checks.append(('arm', [str(root/'docs/deliverables/20261116_hil_retest/arm_negative_checks.py'),
                '--scenario', str(root/'.local/hil/runtime'/selected['runtime']/'rdk'),
                '--output', str(output/'arm-negatives')]))
        if selected['car'] in ('Car0', 'Car3'):
            checks.append(('watchdog', [str(root/'docs/deliverables/20261116_hil_retest/chassis_watchdog_checks.py'),
                '--root', str(root), '--car', selected['car'], '--output', str(output/'chassis-watchdog.json')]))
        checks.append(('navigation', [str(root/'docs/deliverables/20261116_hil_retest/navigation_obstacle_checks.py'),
            '--scenario', str(root/'.local/hil/runtime'/selected['runtime']/'rdk'), '--car', selected['car'],
            '--transmission', selected.get('transmission', 'powershift_auto'),
            '--output', str(output/'navigation-obstacle')]))
        for name, arguments in checks:
            if name not in args.checks:
                continue
            if name in ('sprayer', 'shredder', 'arm', 'visual', 'watchdog', 'navigation', 'existing-spray'):
                backend.agent('pc', 'authorize', '--profile', selected['id'])
                backend.agent('rdk', 'authorize', '--profile', selected['id'])
            emit('running', 'Executing '+name+' physical checks')
            command = agent.ros_command(selected, ['/usr/bin/python3', *arguments])
            with (output/(name+'-console.log')).open('x') as log:
                result = subprocess.run(command, env=agent.environment(selected), stdout=log,
                                        stderr=subprocess.STDOUT, timeout=600)
            report['checks'].append(dict(name=name, returncode=result.returncode))
            persist()
            if result.returncode:
                raise RuntimeError(name+' physical checks failed; see original console/result')
        if selected['car'] == 'Car3' and set(args.checks) & {'sprayer', 'shredder'}:
            kind = 'shredder' if selected['id'] == 'tracked_shredder' else 'sprayer'
            command = agent.ros_command(selected, ['/usr/bin/python3', str(root/
                'docs/deliverables/20261116_hil_retest/work_channel_rejection.py'), '--kind', kind,
                '--output', str(output/'channel-rejection.json')])
            with (output/'channel-rejection-console.log').open('x') as log:
                result = subprocess.run(command, env=agent.environment(selected), stdout=log,
                                        stderr=subprocess.STDOUT, timeout=60)
            report['checks'].append(dict(name='wrong-channel-rejection', returncode=result.returncode))
            if result.returncode:
                raise RuntimeError('Wrong-channel/recovery checks failed; original evidence retained')
        report['passed'] = True
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        # If the verification process was killed during its bounded SIGSTOP,
        # resume only this validated bridge before requesting ordinary disarm.
        for pid in bridge_pids():
            try:
                os.kill(pid, signal.SIGCONT)
            except ProcessLookupError:
                pass
        try:
            backend.safe_stop()
            backend.verify_real_runtime()
            emit('stopped', 'Controller checks ended; outputs disarmed')
        except Exception as error:
            report['passed'] = False
            report['cleanup_error'] = str(error)
            persist()
            raise
        persist()


if __name__ == '__main__':
    main()
