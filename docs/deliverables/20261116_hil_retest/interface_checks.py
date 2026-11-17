"""Read-only identity/bitrate rejection against the actual bound PC USB-CAN."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time


def wait_for_verified_adapter(verify, capture, samples, timeout=8.):
    deadline = time.monotonic()+timeout
    while True:
        try:
            verify()
        except ValueError as error:
            samples.append(dict(at=time.monotonic(), ready=False, error=str(error), link=capture()))
            if str(error) != 'HIL CAN timing/state is not safe for controlled recovery' or time.monotonic() >= deadline:
                raise
            time.sleep(.1)
        else:
            samples.append(dict(at=time.monotonic(), ready=True, link=capture()))
            return


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--bitrate', type=int, choices=(250000, 1000000), required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    root = args.root.resolve()
    source = root/'ros2_ws/src/agribot_hil_apple/agribot_hil_apple/physical_can_guard.py'
    sys.path.insert(0, str(source.parents[1]))
    from agribot_hil_apple.physical_can_guard import verify_adapter
    serial = '003000224856511520343739'
    link, = json.loads(subprocess.check_output(['ip', '-j', '-d', 'link', 'show', 'can0'], text=True))
    active = subprocess.run(['systemctl', '--user', 'is-active', 'agribot-app-hil-pc.service'],
                            capture_output=True, text=True).stdout.strip()
    if active in ('active', 'activating'):
        raise RuntimeError('Read-only negative interface checks require a stopped HIL runtime')
    tx_file = Path('/sys/class/net/can0/statistics/tx_packets')
    before = tx_file.read_text().strip()
    report = dict(passed=False, hardware_outputs=False, link=link, tests=[], initialization_observations=[],
        scope='Actual sysfs/netlink configuration guards only; bidirectional dataflow is audited in mission runs',
        source_sha256=hashlib.sha256(source.read_bytes()).hexdigest())
    try:
        wait_for_verified_adapter(lambda: verify_adapter('can0', serial, args.bitrate),
            lambda: json.loads(subprocess.check_output(['ip', '-j', '-d', 'link', 'show', 'can0'], text=True))[0],
            report['initialization_observations'])
        report['link'] = report['initialization_observations'][-1]['link']
        report['tests'].append(dict(name='bound_identity_and_actual_timing_match', passed=True))
        for name, interface, identity, bitrate in [
                ('wrong_physical_identity', 'can0', 'unbound-test-serial', args.bitrate),
                ('wrong_reviewed_bitrate', 'can0', serial, 1000000 if args.bitrate == 250000 else 250000),
                ('unpermitted_interface', 'can1', serial, args.bitrate),
                ('unreviewed_bitrate', 'can0', serial, 500000)]:
            try:
                verify_adapter(interface, identity, bitrate)
            except ValueError as error:
                report['tests'].append(dict(name=name, passed=True, rejection=str(error)))
            else:
                report['tests'].append(dict(name=name, passed=False, error='Unexpected acceptance'))
        after = tx_file.read_text().strip()
        report['tx_packets_before'], report['tx_packets_after'] = before, after
        report['no_physical_transmissions'] = before == after
        report['passed'] = all(row['passed'] for row in report['tests']) and report['no_physical_transmissions']
    finally:
        args.output.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps({k: v for k, v in report.items() if k != 'link'}, indent=2))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
