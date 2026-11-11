"""Explicit physical test: repeated starts must flash, stop must not abort writes."""
import argparse
import json
from pathlib import Path
import time

from catalog import profile
from cli import STATE, queue_start, read_status, stop
from qualify import stop_and_wait


def probe_processes():
    found = []
    for path in Path('/proc').glob('[0-9]*/comm'):
        try:
            if path.read_text().strip() == 'openocd':
                found.append(int(path.parent.name))
        except (FileNotFoundError, PermissionError, ProcessLookupError):
            continue
    return found


def qualify(identifier, output, repeats=2):
    if output.exists(): raise FileExistsError(output)
    selected = profile(identifier)
    if selected is None: raise ValueError('Unknown reviewed HIL profile')
    output.mkdir(parents=True)
    report = {'passed': False, 'profile': identifier, 'runs': []}
    def persist():
        (output/'qualification.json').write_text(json.dumps(report, indent=2)+'\n')
    try:
        stop_and_wait()
        for _ in range(repeats):
            run_id = queue_start(identifier, selected['algorithm'])['run_id']
            deadline = time.monotonic()+120.
            while time.monotonic() < deadline:
                state = read_status()
                if state.get('run_id') != run_id: raise RuntimeError('HIL session changed')
                pids = probe_processes()
                if state.get('phase') == 'flashing' and pids: break
                if state.get('phase') in ('failed', 'stopped', 'completed', 'configuring_can'):
                    raise RuntimeError('Could not observe the actual flash process before stop')
                time.sleep(.05)
            else: raise TimeoutError('Flash process was not observed')
            entry = {'run_id': run_id, 'stop_requested_during_openocd': pids,
                     'phase_at_stop': state['phase']}
            report['runs'].append(entry)
            persist()
            stop()
            deadline = time.monotonic()+180.
            while time.monotonic() < deadline:
                state = read_status()
                if state.get('phase') == 'stopped': break
                if state.get('phase') == 'failed': raise RuntimeError(state.get('message'))
                time.sleep(.1)
            else: raise TimeoutError('Stop was not confirmed after flash')
            directory = STATE/'runs'/run_id
            flash = (directory/'flash.log').read_text()
            phases = [json.loads(line)['phase'] for line in (directory/'stages.jsonl').read_text().splitlines()]
            if not all(marker in flash for marker in ('Programming Finished', 'Verified OK')):
                raise RuntimeError('Flash was interrupted or did not verify')
            if any(phase in phases for phase in ('configuring_can', 'starting_pc', 'running')):
                raise RuntimeError('Cancelled startup continued beyond flash')
            entry.update(verified=True, stopped=True, mission_started=False)
            stop_and_wait()
            persist()
        if len({item['run_id'] for item in report['runs']}) != repeats:
            raise RuntimeError('Repeated starts reused a previous run')
        report['passed'] = True
        persist()
    except BaseException as error:
        report['error'] = str(error)
        try: stop_and_wait()
        except Exception as cleanup: report['cleanup_error'] = str(cleanup)
        persist()
        raise


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--profile', default='tracked_sprayer')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.execute: qualify(args.profile, args.output)
    else: print(json.dumps({'hardware_actions': False, 'profile': args.profile}))
