"""Read-only fingerprints of the X5 real-vehicle workspace and service files."""
import argparse
import hashlib
import inspect
import json
import os
from pathlib import Path
import subprocess

# These upstream files are unconditionally opened as runtime traces by the
# legacy binary. Keep their changes visible, separately from authored code.
RUNTIME_OUTPUTS = frozenset('agribot_ws/ros2_ws/src/FAST-LIVO2/Log/'+name
                            for name in ('mat_pre.txt', 'mat_out.txt'))


def snapshot(home):
    home = Path(home)
    root = home/'agribot_ws/ros2_ws'
    directories = [root/'src', root/'install', home/'.config/systemd/user']
    files = {}
    for directory in directories:
        if not directory.is_dir():
            if directory == home/'.config/systemd/user':
                files[str(directory.relative_to(home))] = {'directory': False}
                continue
            raise FileNotFoundError(str(directory))
        for folder, children, names in os.walk(directory, followlinks=False):
            children[:] = sorted(name for name in children if name not in
                ('__pycache__', '.git', 'node_modules', '.pytest_cache'))
            for name in sorted(names):
                path = Path(folder)/name
                if path.suffix in ('.pyc', '.log'):
                    continue
                if path.is_symlink():
                    files[str(path.relative_to(home))] = {'symlink': os.readlink(path)}
                elif path.is_file():
                    digest = hashlib.sha256()
                    with path.open('rb') as stream:
                        for block in iter(lambda: stream.read(1024*1024), b''):
                            digest.update(block)
                    files[str(path.relative_to(home))] = {'sha256': digest.hexdigest()}
    if not files:
        raise RuntimeError('No protected real-vehicle files were found')
    return {'schema_version': 1, 'root': str(root), 'files': files}


def compare(before, after, code_only=False):
    left, right = before['files'], after['files']
    return {name: {'before': left.get(name), 'after': right.get(name)}
            for name in sorted(left.keys() | right.keys()) if left.get(name) != right.get(name)
            and not (code_only and name in RUNTIME_OUTPUTS)}


def remote_snapshot(host, known_hosts, identity=None):
    payload = 'import os, json, hashlib\nfrom pathlib import Path\n' + inspect.getsource(snapshot)
    payload += '\nprint(json.dumps(snapshot(Path.home()), sort_keys=True))\n'
    command = ['ssh', '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=8',
               '-o', 'StrictHostKeyChecking=yes', '-o', 'UserKnownHostsFile='+str(known_hosts),
               *(['-i', str(identity), '-o', 'IdentitiesOnly=yes'] if identity else []), host, 'python3 -']
    result = subprocess.run(command, input=payload, text=True, capture_output=True, timeout=240)
    if result.returncode:
        raise RuntimeError('Read-only X5 fingerprint failed: '+result.stderr[-2000:])
    return json.loads(result.stdout)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host', required=True)
    parser.add_argument('--known-hosts', type=Path, required=True)
    parser.add_argument('--identity', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--compare', type=Path)
    parser.add_argument('--code-only', action='store_true', help='List the two legacy runtime traces separately')
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    result = remote_snapshot(args.host, args.known_hosts, args.identity)
    if args.compare:
        before = json.loads(args.compare.read_text())
        all_changes = compare(before, result)
        result['changes'] = compare(before, result, code_only=args.code_only)
        result['runtime_output_changes'] = {name: value for name, value in all_changes.items()
                                           if name not in result['changes']}
        result['unchanged'] = not result['changes']
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, sort_keys=True)+'\n')
    print(json.dumps({'output': str(args.output), 'files': len(result['files']),
                      'unchanged': result.get('unchanged')}))
    if args.compare and not result['unchanged']:
        raise SystemExit('Protected real-vehicle files have changed; HIL must not start')


if __name__ == '__main__':
    main()
