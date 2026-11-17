"""Re-run independent audits only on the explicitly selected closed fresh jobs."""
import argparse
import json
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--directory', type=Path, required=True)
    parser.add_argument('--jobs', type=Path, required=True)
    args = parser.parse_args()
    root, directory = args.root.resolve(), args.directory.resolve()
    sys.path.insert(0, str(root/'tools/hil'))
    from agent import Agent
    from catalog import profile
    agent = Agent(root/'.local/hil', 'pc')
    scripts = root/'docs/deliverables/20261116_hil_retest'
    campaign = json.loads((directory/'result.json').read_text())
    if not campaign.get('passed'):
        raise RuntimeError('Fresh physical campaign must have closed successfully before its final independent audit')
    commands = []
    for job in json.loads(args.jobs.read_text()):
        folder = directory/job['name']
        command = None
        if job['kind'] == 'mission' and job['profile'] == 'ackermann_apple':
            command = ['/usr/bin/python3', str(scripts/'review_apple.py'), '--directory', str(folder)]
        if job['kind'] == 'controller' and job['profile'].startswith('tracked_'):
            kind = 'sprayer' if job['profile'] == 'tracked_sprayer' else 'shredder'
            command = ['/usr/bin/python3', str(scripts/'review_tracked.py'),
                '--physical', str(folder/(kind+'.json')), '--usb', str(folder/'hardware-evidence/x5/usb-can.jsonl'),
                '--kind', kind, '--output', str(folder/'feedback-independent-audit.json')]
        if command:
            output = folder/'independent-review-console.log'
            if output.exists():
                raise FileExistsError(output)
            with output.open('x') as stream:
                code = subprocess.run(agent.ros_command(profile(job['profile']), command),
                    env=agent.environment(profile(job['profile'])), stdout=stream, stderr=subprocess.STDOUT, timeout=120).returncode
            commands.append(dict(job=job['name'], command=command, returncode=code))
            if code:
                raise RuntimeError('Fresh closed-run audit failed: '+job['name'])
    (directory/'independent-reviews.json').write_text(json.dumps(dict(passed=True, steps=commands), indent=2)+'\n')


if __name__ == '__main__':
    main()
