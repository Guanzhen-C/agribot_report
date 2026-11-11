"""Prepare an isolated HIL runtime without deploying into the real X5 workspace."""
import argparse
import json
from pathlib import Path
import shlex
import subprocess

from catalog import ROOT
from config import load, ssh_arguments
from protect_real_runtime import compare, remote_snapshot


def run(command):
    subprocess.run(list(map(str, command)), check=True)


def deploy(pc=True, rdk=True):
    config = load()
    state = ROOT/'.local/hil'
    baseline = json.loads(Path(config['real_runtime_fingerprint']).read_text())
    before = remote_snapshot(config['host'], Path(config['known_hosts']), Path(config['identity']))
    if compare(baseline, before, code_only=True):
        raise RuntimeError('Protected real X5 runtime changed; deployment refused')
    if pc:
        run(['/usr/bin/python3', ROOT/'tools/hil/prepare_runtime.py', '--root', state,
             '--code', ROOT, '--role', 'pc', '--underlay', ROOT/'ros2_ws/install/setup.bash'])
    if rdk:
        remote = config['remote_root']
        # A remote symlink must not redirect our destination into real source.
        run(ssh_arguments(config, 'python3', '-c',
            'from pathlib import Path; p=Path("'+remote+'"); '
            'assert not p.is_symlink(); assert p.resolve()==Path.home()/"agribot_app_hil_ws"; '
            'p.mkdir(exist_ok=True)'))
        transport = shlex.join(ssh_arguments(config)[:-2])
        for relative in ('tools/hil', 'ros2_ws/src/agribot_hil_apple', 'ros2_ws/src/agribot_hil_hitch',
                         'ros2_ws/src/FAST-LIVO2',
                         'resources/hil/scenarios', 'unity/Assets/AgribotConfig/Resources/hil_profiles.json'):
            # --relative preserves only these explicit source subtrees. No field
            # sources, install directories, passwords or user session state travel.
            source = str(ROOT)+'/./'+relative+('/' if (ROOT/relative).is_dir() else '')
            run(['rsync', '-aR', '--safe-links', '--exclude=__pycache__', '--exclude=*.pyc', '--exclude=Log/',
                 '--exclude=*.meta', '-e', transport, source, config['host']+':'+remote+'/'])
        # X5 need not contact GitHub. Transfer only the pinned public build
        # dependencies already fetched on the PC, not another application copy.
        for name in ('AgIsoStack-plus-plus', 'IFRA_LinkAttacher'):
            source = state/'external'/name
            if not (source/'.git').is_dir():
                raise FileNotFoundError('Prepare the PC HIL overlay first: '+str(source))
            run(['rsync', '-aR', '--safe-links', '-e', transport,
                 str(state)+'/./external/'+name+'/', config['host']+':'+remote+'/'])
        run(ssh_arguments(config, 'python3', remote+'/tools/hil/prepare_runtime.py', '--root', remote,
                          '--code', remote, '--role', 'rdk',
                          '--underlay', '/home/sunrise/agribot_ws/ros2_ws/install/setup.bash'))
    after = remote_snapshot(config['host'], Path(config['known_hosts']), Path(config['identity']))
    changes = compare(baseline, after, code_only=True)
    all_changes = compare(baseline, after)
    report = {'real_code_unchanged': not changes, 'files': len(after['files']), 'changes': changes,
              'runtime_output_changes': {name: value for name, value in all_changes.items() if name not in changes}}
    (state/'deployment-check.json').write_text(json.dumps(report, indent=2)+'\n')
    if changes:
        raise RuntimeError('Protected real X5 runtime changed during HIL preparation')
    print(json.dumps(report))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--only', choices=('pc', 'rdk'))
    args = parser.parse_args()
    deploy(pc=args.only != 'rdk', rdk=args.only != 'pc')
