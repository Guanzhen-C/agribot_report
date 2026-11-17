"""Five inert invalid-telemetry frames, only on a fully stopped isolated bench."""
import argparse
import json
from pathlib import Path
import socket
import struct
import subprocess
import sys
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--bitrate', type=int, choices=(250000, 1000000), required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    state = json.loads((args.root/'.local/hil/state.json').read_text())
    if state.get('phase') != 'stopped' or state.get('session_alive'):
        raise RuntimeError('Neutral diagnostic requires confirmed stopped state')
    for name in ('session', 'pc', 'stop'):
        result = subprocess.run(['systemctl', '--user', 'is-active', 'agribot-app-hil-'+name+'.service'],
                                capture_output=True, text=True)
        if result.stdout.strip() in ('active', 'activating', 'deactivating'):
            raise RuntimeError('A HIL unit is still active')
    sys.path.insert(0, str(args.root/'ros2_ws/src/agribot_hil_apple'))
    from agribot_hil_apple.physical_can_guard import verify_adapter
    verify_adapter('can0', '003000224856511520343739', args.bitrate)
    link, = json.loads(subprocess.check_output(['ip', '-j', '-d', 'link', 'show', 'can0'], text=True))
    if link['linkinfo']['info_data'].get('ctrlmode', {}).get('loopback'):
        raise RuntimeError('Physical diagnostic must not use internal loopback')
    counter = Path('/sys/class/net/can0/statistics/tx_packets')
    bus = socket.socket(socket.PF_CAN, socket.SOCK_RAW, socket.CAN_RAW)
    bus.bind(('can0',))
    bus.settimeout(.2)
    frame = struct.Struct('=IB3x8s')
    report = dict(passed=False, attempts=[], link=link,
        scope='Physical kernel TX completion only, not bidirectional mission acceptance',
        payload_semantics='All-zero invalid sensor telemetry, never a motion/implement command; all HIL receivers stopped')
    try:
        before = int(counter.read_text())
        for index in range(5):
            sent_at = time.monotonic()
            bus.send(frame.pack(0x103 if args.bitrate == 1000000 else 0x621, 8, bytes(8)))
            time.sleep(.2)
            after = int(counter.read_text())
            report['attempts'].append(dict(index=index, sent_monotonic=sent_at,
                tx_packets_before=before, tx_packets_after=after, completion=after > before))
            before = after
        report['passed'] = all(row['completion'] for row in report['attempts'])
    finally:
        bus.close()
        args.output.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
