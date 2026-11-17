"""Cross-check actual two-fruit picking, native travel and gripper retention."""
import argparse
import hashlib
import inspect
import json
from pathlib import Path

from agribot_hil_apple import audit_mission, gripper_audit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, required=True)
    args = parser.parse_args()
    directory = args.directory
    output = directory/'apple-independent-audit.json'
    if output.exists():
        raise FileExistsError(output)
    paths = [directory/'hardware-evidence/x5/mission'/name for name in ('mission.json', 'harvest.json')]
    paths.append(directory/'independent/independent-pc.json')
    rows = [json.loads(p.read_text()) for p in paths]
    report = dict(scope='Offline actual contact/picking/retention and strict half-wheelbase audit',
        physical_picking=audit_mission.audit(*rows),
        transit_gripper=gripper_audit.audit(*rows),
        source_sha256={str(Path(inspect.getfile(module))): hashlib.sha256(
            Path(inspect.getfile(module)).read_bytes()).hexdigest() for module in (audit_mission, gripper_audit)},
        input_sha256={str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
    report['passed'] = all(report[name]['status'] == 'passed' for name in ('physical_picking', 'transit_gripper'))
    output.write_text(json.dumps(report, indent=2, allow_nan=False)+'\n')
    print(json.dumps(report, indent=2, allow_nan=False))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
