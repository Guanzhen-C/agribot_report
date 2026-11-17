"""Archive a stopped run and apply both original and independent power audits."""
import argparse
import json
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--directory', type=Path, required=True)
    args = parser.parse_args()
    root, directory = args.root.resolve(), args.directory.resolve()
    result_path = directory/'postcheck.json'
    if result_path.exists():
        raise FileExistsError(result_path)
    mission = json.loads((directory/'mission-checks.json').read_text())
    scripts = root/'docs/deliverables/20261116_hil_retest'
    report = dict(run_id=mission['run_id'], profile=mission['profile'], steps=[], passed=False,
                  scope='Recorded mission cross-check, not full 30-case acceptance')
    evidence = directory/'hardware-evidence'
    commands = [[sys.executable, str(scripts/'collect_run.py'), '--root', str(root),
                 '--run', mission['run_id'], '--output', str(evidence)]]
    if mission['profile'].startswith('tractor_'):
        commands.extend([
            [sys.executable, str(root/'ros2_ws/src/agribot_hil_hitch/scripts/summarize_powertrain.py'),
             '--pc', str(evidence/'pc/pc-can.jsonl'), '--usb', str(evidence/'x5/rdk-can.jsonl'),
             '--run', str(evidence/'x5/mission'), '--output', str(directory/'powertrain-audit.json'),
             '--plot', str(directory/'powertrain-curves.png')],
            [sys.executable, str(scripts/'review_powertrain.py'), '--run-directory', str(directory),
             '--output', str(directory/'powertrain-independent-audit.json')]])
    if (directory/'stamped-poses.jsonl').exists():
        commands.append([sys.executable, str(scripts/'pose_audit.py'), '--analyze',
                         str(directory/'stamped-poses.jsonl'), '--output', str(directory/'pose-audit.json')])
    try:
        for index, command in enumerate(commands):
            with (directory/f'postcheck-{index}-console.log').open('x') as stream:
                code = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT,
                                      timeout=300).returncode
            report['steps'].append(dict(command=command, returncode=code))
            # A failing original audit still needs the independent audit.
            if index == 0 and code:
                break
        report['passed'] = bool(mission.get('passed') and len(report['steps']) == len(commands)
                                and all(s['returncode'] == 0 for s in report['steps']))
    finally:
        result_path.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
