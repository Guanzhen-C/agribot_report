"""Fresh complementary checks between physical campaigns, never during a mission."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--execute', action='store_true')
    args = parser.parse_args()
    if not args.execute:
        raise SystemExit('Explicit --execute is required for physical firmware repeat/cancellation checks')
    root, output = args.root.resolve(), args.output.resolve()
    sys.path.insert(0, str(root/'tools/hil'))
    from agent import Agent
    from catalog import profile
    from cli import read_status
    from qualify import stop_and_wait
    if read_status().get('phase') != 'stopped':
        raise RuntimeError('Previous physical campaign must have fully stopped')
    output.mkdir(parents=True, exist_ok=False)
    agent = Agent(root/'.local/hil', 'pc')
    selected = profile('tracked_sprayer')
    scripts = root/'docs/deliverables/20261116_hil_retest'
    report = dict(passed=False, started_at=datetime.datetime.now(datetime.timezone.utc).isoformat(), steps=[])

    def run(name, command, environment=None):
        with (output/(name+'.log')).open('x') as stream:
            code = subprocess.run(command, env=environment, stdout=stream, stderr=subprocess.STDOUT, timeout=600).returncode
        report['steps'].append(dict(name=name, command=command, returncode=code))
        if code:
            raise RuntimeError(name+' failed; retained original log')

    try:
        run('flash-repeat-cancel', ['/usr/bin/python3', str(root/'tools/hil/qualify_cancel.py'),
            '--execute', '--profile', 'ackermann_apple', '--output', str(output/'flash-repeat-cancel')])
        run('negative-preflight', ['/usr/bin/python3', str(scripts/'negative_preflight.py'),
            '--root', str(root), '--output', str(output/'negative-preflight')])
        for bitrate, name in [(1000000, 'interfaces-1m'), (250000, 'interfaces-250k')]:
            if bitrate == 250000:
                # Both physical endpoints must match before the positive probe;
                # changing only the PC bitrate would deliberately corrupt CAN.
                run('flash-switch-250k', ['/usr/bin/python3', str(root/'tools/hil/qualify_cancel.py'),
                    '--execute', '--profile', 'tracked_sprayer', '--output', str(output/'flash-switch-250k')])
            run('configure-'+name, ['sudo', '-n', '/usr/local/libexec/agribot-hil-bus', 'pc', str(bitrate)])
            run(name, ['/usr/bin/python3', str(scripts/'interface_checks.py'), '--root', str(root),
                '--bitrate', str(bitrate), '--output', str(output/(name+'.json'))])
        run('scene-attestation', ['/usr/bin/python3', str(scripts/'scene_attestation.py'),
            '--root', str(root), '--output', str(output/'scene-attestation.json')])
        env = agent.environment(selected)
        env.pop('CYCLONEDDS_URI', None)
        env.update(ROS_DOMAIN_ID='225', ROS_LOCALHOST_ONLY='1')
        run('controller-patience', agent.ros_command(selected, ['/usr/bin/python3',
            str(scripts/'controller_patience.py'), '--config', str(root/'.local/hil/runtime/tracked_sprayer/rdk/nav2.yaml'),
            '--output', str(output/'controller-patience')]), env)
        report['passed'] = True
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        stop_and_wait()
        report['finished_at'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        report['source_sha256'] = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in [Path(__file__), scripts/'controller_patience.py', root/'tools/hil/qualify_cancel.py']}
        (output/'result.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
