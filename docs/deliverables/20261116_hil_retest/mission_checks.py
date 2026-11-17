"""Run a fresh default physical mission with passive sensors and terminal checks."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--profile', required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--execute', action='store_true')
    args = parser.parse_args()
    if not args.execute:
        print('No hardware actions without --execute')
        return
    root = args.root.resolve()
    scripts = root/'docs/deliverables/20261116_hil_retest'
    sys.path.insert(0, str(root/'tools/hil'))
    from agent import Agent
    from catalog import profile
    from cli import queue_start, read_status
    from config import load
    from protect_real_runtime import compare, remote_snapshot
    from qualify import inspect_run, stop_and_wait
    selected = profile(args.profile)
    if selected is None:
        raise ValueError('Unsupported profile')
    args.output.mkdir(parents=True, exist_ok=False)
    agent = Agent(root/'.local/hil', 'pc')
    config = load()
    baseline = remote_snapshot(config['host'], Path(config['known_hosts']), Path(config['identity']))
    children, handles = [], []
    report = dict(scope='Fresh physical default mission, sensor and post-goal checks; not 30-case acceptance',
                  profile=args.profile, passed=False)

    def persist():
        (args.output/'mission-checks.json').write_text(json.dumps(report, indent=2)+'\n')

    def spawn(name, command, ros=False):
        handle = (args.output/(name+'-console.log')).open('x')
        handles.append(handle)
        if ros:
            command = agent.ros_command(selected, command)
        child = subprocess.Popen(command, env=agent.environment(selected), stdout=handle,
                                 stderr=subprocess.STDOUT, start_new_session=True)
        children.append(child)
        return child

    def joined(child, timeout):
        code = child.wait(timeout=timeout)
        if code:
            raise RuntimeError('Passive test process failed with exit '+str(code)+'; see its original log')

    try:
        folders = ['tools/hil', 'ros2_ws/src/agribot_hil_apple', 'ros2_ws/src/agribot_hil_hitch',
                   'docs/deliverables/20261116_hil_retest']
        fingerprints = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
                        for folder in folders for p in sorted((root/folder).rglob('*'))
                        if p.is_file() and '__pycache__' not in p.parts
                        and p.suffix in ('.py', '.cpp', '.c', '.h', '.yaml', '.xml')}
        patch = root/'ros2_ws/src/agribot_hil_apple/patches/nav2_humble_mppi_failure.patch'
        fingerprints[str(patch.relative_to(root))] = hashlib.sha256(patch.read_bytes()).hexdigest()
        (args.output/'source-fingerprints.json').write_text(json.dumps(dict(
            scope='PC source files captured before starting this run; not a clean Git commit or X5 installed-code attestation',
            sha256=fingerprints), indent=2)+'\n')
        stop_and_wait()
        state = queue_start(args.profile, selected['algorithm'], ['Radar', 'RTK', 'Camera', 'Imu'])
        run = state['run_id']
        report['run_id'] = run
        persist()
        spawn('stamped-poses', ['/usr/bin/python3', str(scripts/'pose_audit.py'), '--car', selected['car'],
              '--output', str(args.output/'stamped-poses.jsonl')], ros=True)
        observer = spawn('observer', ['/usr/bin/python3', str(scripts/'observe_run.py'), '--root', str(root),
            '--run', run, '--profile', args.profile, '--output', str(args.output/'independent')])
        held_guard = None
        if selected['car'] == 'Car0':
            held_guard = spawn('held-guard', ['/usr/bin/python3', str(scripts/'held_fruit_guard.py'),
                '--scenario', str(root/'.local/hil/runtime'/selected['runtime']/'rdk'), '--output',
                str(args.output/'held-fruit-guard.json')], ros=True)
        sensor, last, attested, harrow = None, None, False, None
        deadline = time.monotonic()+2400.
        while time.monotonic() < deadline:
            state = read_status()
            if state.get('run_id') != run:
                raise RuntimeError('HIL session changed during test')
            if state.get('phase') != last:
                print(json.dumps(state, ensure_ascii=False), flush=True)
                last = state.get('phase')
            if last == 'running' and sensor is None:
                command = ['/usr/bin/python3', str(scripts/'sensor_audit.py'), '--output',
                           str(args.output/'sensor-audit.json'), '--seconds', '30']
                if selected['car'] == 'Car4':
                    command.append('--no-camera')
                sensor = spawn('sensors', command, ros=True)
                if selected['car'] == 'Car4':
                    harrow = spawn('harrow-history', ['/usr/bin/python3', str(scripts/'harrow_history.py'),
                        '--state', str(root/'.local/hil/state.json'), '--run', run,
                        '--output', str(args.output/'harrow-history')], ros=True)
            if last == 'running' and not attested:
                with (args.output/'runtime-attestation-console.log').open('x') as stream:
                    subprocess.run(['/usr/bin/python3', str(scripts/'runtime_attestation.py'),
                        '--root', str(root), '--run', run, '--output',
                        str(args.output/'runtime-attestation.json')], check=True,
                        stdout=stream, stderr=subprocess.STDOUT, timeout=50.)
                attested = True
            if last == 'completed':
                break
            if last in ('failed', 'stopped'):
                raise RuntimeError(state.get('message', 'HIL mission failed'))
            time.sleep(.5)
        else:
            raise TimeoutError('Default mission exceeded test deadline')
        report['mission'] = inspect_run(selected, run)
        if sensor is None:
            raise RuntimeError('No running interval for sensor audit')
        joined(sensor, 60.)
        manifest = json.loads((root/'.local/hil/runtime'/selected['runtime']/'rdk/mission.json').read_text())
        goal = ([manifest['goals'][-1]['x'], manifest['goals'][-1]['y']] if selected['car'] == 'Car4'
                else manifest['finish'][:2])
        hold = spawn('terminal', ['/usr/bin/python3', str(scripts/'terminal_hold.py'), '--root', str(root),
            '--run', run, '--car', selected['car'], '--goal', *map(str, goal), '--output',
            str(args.output/'terminal.json')], ros=True)
        joined(hold, 100.)
        joined(observer, 30.)
        if harrow is not None:
            joined(harrow, 30.)
        if held_guard is not None:
            joined(held_guard, 30.)
        report['passed'] = True
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        for child in children:
            if child.poll() is None:
                os.killpg(child.pid, signal.SIGINT)
                try:
                    child.wait(timeout=15.)
                except subprocess.TimeoutExpired:
                    os.killpg(child.pid, signal.SIGTERM)
                    child.wait(timeout=15.)
        for handle in handles:
            handle.close()
        try:
            stop_and_wait()
            actual = remote_snapshot(config['host'], Path(config['known_hosts']), Path(config['identity']))
            report['real_files_changed'] = compare(baseline, actual)
            if report['real_files_changed']:
                raise RuntimeError('Protected real vehicle files changed')
        except Exception as error:
            report['passed'] = False
            report['cleanup_error'] = str(error)
            raise
        finally:
            persist()


if __name__ == '__main__':
    main()
