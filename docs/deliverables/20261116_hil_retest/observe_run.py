"""Attach the reviewed passive recorders to a fresh physical qualification run."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--run', required=True)
    parser.add_argument('--profile', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    sys.path.insert(0,str(root/'tools/hil'))
    from agent import Agent
    agent = Agent(root/'.local/hil','pc')
    selected = agent.select(args.profile)
    args.output.mkdir(parents=True,exist_ok=False)
    scripts = Path(__file__).resolve().parent
    source = scripts/('passive_apple.py' if args.profile == 'ackermann_apple' else 'passive_profiles.py')
    commands = ['/usr/bin/python3', str(source), '--root', str(root), '--run', args.run,
                '--output', str(args.output)]
    if args.profile != 'ackermann_apple':
        commands.extend(['--profile', args.profile])
    manifest = dict(scope='Passive subscriptions and CAN reception only',run_id=args.run,
                    recorder=str(source),recorder_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                    original_output=str(args.output))
    child = stream = extra_stream = None
    try:
        if args.profile.startswith('tracked_'):
            extra_stream = (args.output/'costmap-console.log').open('x')
            probe = root/'docs/deliverables/20261116_hil_retest/collision_probe.py'
            manifest['collision_recorder_sha256'] = hashlib.sha256(probe.read_bytes()).hexdigest()
            child = subprocess.Popen(agent.ros_command(selected,['/usr/bin/python3',str(probe),
                '--output',str(args.output/'raw-collisions.jsonl')]),
                env=agent.environment(selected),stdout=extra_stream,stderr=subprocess.STDOUT)
        with (args.output/'observer.log').open('x') as log:
            result = subprocess.run(agent.ros_command(selected,commands),env=agent.environment(selected),
                                    stdout=log,stderr=subprocess.STDOUT,timeout=2500)
            manifest['recorder_returncode'] = result.returncode
    finally:
        if child is not None:
            if child.poll() is None:
                child.terminate()
            child.wait(timeout=15)
            manifest['costmap_recorder_returncode'] = child.returncode
        if extra_stream: extra_stream.close()
        (args.output/'recorder-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')


if __name__=='__main__':
    main()
