"""Repeat reviewed physical HIL missions, retaining flash and task evidence."""
import argparse
import json
from pathlib import Path
import subprocess
import time

from catalog import ROOT, firmware, profile
from cli import STATE, STOP_UNIT, queue_start, read_status, stop, unit_active
from config import load
from protect_real_runtime import compare, remote_snapshot


def wait_terminal(run_id, timeout):
    deadline, last = time.monotonic()+timeout, None
    while time.monotonic() < deadline:
        value = read_status()
        if value.get('run_id') != run_id:
            raise RuntimeError('HIL session changed while qualification was running')
        if value.get('phase') != last:
            last = value.get('phase')
            print(json.dumps(value, ensure_ascii=False), flush=True)
        if value.get('phase') in ('completed', 'stopped', 'failed'):
            if value['phase'] != 'completed':
                raise RuntimeError(value.get('message', 'HIL did not complete'))
            return value
        time.sleep(1.)
    raise TimeoutError('Physical HIL mission exceeded the qualification deadline')


def stop_and_wait():
    stop()
    deadline = time.monotonic()+90.
    while time.monotonic() < deadline:
        if read_status().get('phase') == 'stopped' and not unit_active() and not unit_active(STOP_UNIT):
            return
        time.sleep(.25)
    raise TimeoutError('HIL cleanup was not confirmed; no next profile was started')


def inspect_run(selected, run_id):
    directory = STATE/'runs'/run_id
    image = json.loads((directory/'firmware.json').read_text())
    expected = firmware(selected['firmware'])
    if image['sha256'] != expected['sha256']:
        raise ValueError('Recorded firmware does not match the selected profile')
    flash = (directory/'flash.log').read_text()
    if 'Verified OK' not in flash or 'Programming Finished' not in flash:
        raise ValueError('Actual flash verification evidence is missing')
    checks = [json.loads(line) for line in (directory/'real_runtime_checks.jsonl').read_text().splitlines()]
    if len(checks) < 3 or not all(item['unchanged'] for item in checks):
        raise ValueError('Real vehicle code protection was not confirmed at all gates')
    result = json.loads((directory/'mission-result.json').read_text())
    task = result['result']
    if task.get('passed') is not True and task.get('status') not in ('succeeded', 'completed'):
        raise ValueError('Successful actual X5 mission evidence is missing')
    return {'profile': selected['id'], 'run_id': run_id, 'firmware_sha256': image['sha256'],
            'bitrate': image['bitrate'], 'passed': True, 'evidence': str(directory)}


def qualify(identifiers, output, wait_current=False, timeout=1800.):
    if output.exists(): raise FileExistsError(output)
    output.mkdir(parents=True)
    config = load()
    baseline = remote_snapshot(config['host'], Path(config['known_hosts']), Path(config['identity']))
    report = {'scope': 'Actual X5/6750/USB-CAN HIL', 'passed': False, 'runs': [],
              'source_dirty': bool(subprocess.check_output(['git', '-C', ROOT, 'status', '--porcelain'], text=True)),
              'source_commit': subprocess.check_output(['git', '-C', ROOT, 'rev-parse', 'HEAD'], text=True).strip()}
    def persist():
        (output/'qualification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    try:
        if wait_current and read_status().get('phase') not in ('stopped', 'completed'):
            wait_terminal(read_status()['run_id'], timeout)
        for identifier in identifiers:
            selected = profile(identifier)
            if selected is None: raise ValueError('Unsupported qualification profile')
            stop_and_wait()
            run_id = queue_start(identifier, selected['algorithm'])['run_id']
            report['current_profile'], report['current_run_id'] = identifier, run_id
            persist()
            wait_terminal(run_id, timeout)
            report['runs'].append(inspect_run(selected, run_id))
            persist()
        stop_and_wait()
        actual = remote_snapshot(config['host'], Path(config['known_hosts']), Path(config['identity']))
        report['real_files_changed_during_qualification'] = compare(baseline, actual)
        if report['real_files_changed_during_qualification']:
            raise RuntimeError('A real workspace file changed during qualification, including runtime traces')
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
    parser.add_argument('--execute', action='store_true', help='Required: flash and run actual bench hardware')
    parser.add_argument('--profiles', nargs='+', required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--wait-current', action='store_true')
    parser.add_argument('--timeout', type=float, default=1800.)
    args = parser.parse_args()
    if not args.execute:
        print(json.dumps({'hardware_actions': False, 'profiles': args.profiles}))
    else:
        qualify(args.profiles, args.output, args.wait_current, args.timeout)
