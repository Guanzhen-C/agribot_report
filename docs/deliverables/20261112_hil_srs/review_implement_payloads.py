"""Read saved physical CAN traces only; never open a CAN or ROS interface."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path


def review(root):
    result = {}
    for run, identities in [
        ('20261112T115629_2e6203ef', [0x5b0, 0x5b2, 0x5b3]),
        ('20261112T121003_535a1379', [0x5b0, 0x5b4])
    ]:
        native = root/'.validation/hil_other_profiles_20261112_r1'/run/'native-can.jsonl'
        usb = root/'.validation/hil_notebook_20261112'/run/'x5/usb-can.jsonl'
        received = json.loads(usb.read_text())
        left = {identity: Counter() for identity in identities}
        right = {identity: Counter() for identity in identities}
        for line in native.open():
            row = json.loads(line)
            if row[2] in left and row[3] == 8:
                left[row[2]][row[4]] += 1
        for row in received['received']:
            if not row[2]:
                continue
            pgn = row[1] >> 8 & 0x3ffff
            identity = 0x5b0 if pgn == 0xff50 else (
                0x5b2+pgn-0xff52 if 0xff52 <= pgn <= 0xff54 else None)
            if identity in right:
                right[identity][row[3]] += 1
        result[run] = {
            'scope': 'Read-only retrospective payload multiset correlation, not synchronized '
                     'per-frame latency or zero-loss certification',
            'feedback': {
                f'{identity:03X}': {
                    'observed_native_frames': sum(left[identity].values()),
                    'received_isobus_frames': sum(right[identity].values()),
                    'payload_multiset_matches': sum((left[identity] & right[identity]).values()),
                    'unmatched_received': sum((right[identity]-left[identity]).values())
                } for identity in identities
            },
            'usb_decoder_invalid': received['decoder_invalid'], 'usb_errors': received['errors'],
            'sha256': {str(path.relative_to(root)): hashlib.sha256(path.read_bytes()).hexdigest()
                       for path in (native, usb)}
        }
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(review(args.root), indent=2))
