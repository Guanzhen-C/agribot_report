"""Offline three-way native/ISOBUS payload agreement, never a zero-loss claim."""
import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path


def pgn(identifier):
    value = (identifier >> 8) & 0x3ffff
    return value & 0x3ff00 if ((identifier >> 16) & 255) < 240 else value


def valid(data):
    try:
        payload = bytes.fromhex(data)
    except ValueError:
        return False
    checksum = 0
    for byte in payload[:7]:
        checksum ^= byte
    return len(payload) == 8 and checksum == payload[7]


def analyze(physical, usb, kind):
    identifiers = (0x5b4,) if kind == 'shredder' else (0x5b2, 0x5b3)
    expected = {3: 3500} if kind == 'shredder' else {1: 3, 2: 4}
    native = [row for row in physical['records'] if row['kind'] == 'native_can']
    feedback = [row for row in physical['records'] if row['kind'] == 'isobus'
                and row['value'].get('kind') == 'feedback' and row['value'].get('id') in identifiers]
    lookup = defaultdict(list)
    for row in native:
        lookup[row['value']['id'], row['value']['data']].append(row['wall'])
    wire = Counter((pgn(row[1]), row[3]) for row in usb['received'] if row[2])
    sent_values = set()
    invalid_commands = []
    for row in usb['sent']:
        if not row[2] or pgn(row[1]) != 0xef00:
            continue
        data = row[3]
        if not valid(data):
            invalid_commands.append(row)
            continue
        payload = bytes.fromhex(data)
        if payload[0:2] == b'\x01\x01' and payload[6] == 1:
            sent_values.add((payload[3], int.from_bytes(payload[4:6], 'little')))
    results = {}
    for identifier in identifiers:
        received = [row for row in feedback if row['value']['id'] == identifier]
        frames = [row for row in native if row['value']['id'] == identifier]
        first = min((row['wall'] for row in frames), default=0.)+.5
        last = max((row['wall'] for row in frames), default=0.)-.5
        interior = [row for row in received if first <= row['wall'] <= last]
        unmatched, ambiguous = [], 0
        used = Counter()
        for row in interior:
            data = row['value']['data']
            candidates = [stamp for stamp in lookup[identifier, data] if abs(row['wall']-stamp) <= .5]
            converted_pgn = 0xff52+identifier-0x5b2
            used[converted_pgn, data] += 1
            if not candidates or not valid(data) or wire[converted_pgn, data] < used[converted_pgn, data]:
                unmatched.append(row)
            ambiguous += len(candidates) > 1
        results[hex(identifier)] = dict(native_samples=len(frames), received_ros_samples=len(received),
            common_window_samples=len(interior), unmatched_samples=unmatched,
            repeated_payload_ambiguous_samples=ambiguous,
            passed=bool(frames and interior and not unmatched))
    checks = dict(original_physical_checks_passed=physical.get('ok') is True,
        actual_upstream_nonzero_fields_present=all(item in sent_values for item in expected.items()),
        upstream_application_checksums_valid=not invalid_commands,
        native_feedback_equals_physical_isobus_and_received_ros=all(row['passed'] for row in results.values()),
        no_cdc_decoder_or_transport_errors=not usb.get('errors') and usb.get('decoder_invalid') == 0)
    return dict(passed=all(checks.values()), checks=checks, feedback=results,
        comparison_basis='Native CAN and received ROS events share one PC observer clock; raw X5 USB independently proves PGN/payload reception',
        limitations='4-bit counters and repeated payloads can be ambiguous. Counts are received/matched records, NOT a zero-loss or cross-host latency assertion.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--physical', type=Path, required=True)
    parser.add_argument('--usb', type=Path, required=True)
    parser.add_argument('--kind', choices=('sprayer', 'shredder'), required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    report = analyze(json.loads(args.physical.read_text()), json.loads(args.usb.read_text()), args.kind)
    report['sha256'] = {str(path): hashlib.sha256(path.read_bytes()).hexdigest()
                        for path in (args.physical, args.usb, Path(__file__))}
    args.output.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
