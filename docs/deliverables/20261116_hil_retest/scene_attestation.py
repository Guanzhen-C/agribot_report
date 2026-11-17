"""Read-only cross-host hashes of frozen navigation, map and task inputs."""
import argparse
import hashlib
import inspect
import json
from pathlib import Path
import subprocess
import sys


def scan(runtime):
    import hashlib
    from pathlib import Path
    result = {}
    for folder in ('ackermann_apple', 'tracked_sprayer', 'tracked_shredder', 'tractor_harrow'):
        root = Path(runtime)/folder/'rdk'
        files = {}
        for path in sorted(root.rglob('*')):
            if not path.is_file() or '__pycache__' in path.parts:
                continue
            data = path.read_bytes()
            normalized = data
            if path.suffix in ('.json', '.yaml', '.urdf', '.sdf'):
                normalized = data.replace(str(Path(runtime)).encode(), b'${HIL_RUNTIME}')
            files[str(path.relative_to(root))] = dict(bytes=len(data),
                raw_sha256=hashlib.sha256(data).hexdigest(),
                path_normalized_sha256=hashlib.sha256(normalized).hexdigest())
        result[folder] = files
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    root = args.root.resolve()
    sys.path.insert(0, str(root/'tools/hil'))
    from config import load, ssh_arguments
    from catalog import load as catalog
    config = load(validate_flash=False)
    local = scan(root/'.local/hil/runtime')
    code = 'import json\n'+inspect.getsource(scan)+'\nprint(json.dumps(scan('+repr(config['remote_root']+'/runtime')+')))'
    remote = json.loads(subprocess.check_output(ssh_arguments(config, '/usr/bin/python3', '-c', code),
                                               text=True, timeout=60.))
    differences, normalized_paths = [], []
    for runtime in local:
        a, b = local[runtime], remote.get(runtime, {})
        for name in sorted(a.keys() | b.keys()):
            if name not in a or name not in b:
                differences.append(dict(runtime=runtime, file=name, reason='File missing on one host'))
                continue
            if a[name]['path_normalized_sha256'] != b[name]['path_normalized_sha256']:
                differences.append(dict(runtime=runtime, file=name, reason='Content differs after exact runtime-root normalization'))
            elif a[name]['raw_sha256'] != b[name]['raw_sha256']:
                normalized_paths.append(dict(runtime=runtime, file=name))
    essential = ('mission.json', 'map.yaml', 'map.pgm', 'nav2.yaml')
    complete = all(all(name in values for name in essential) for values in local.values())
    report = dict(passed=bool(complete and not differences),
        scope='Read-only deployed PC/X5 rdk inputs, including map pixels, goals, navigation and sensor settings',
        normalization='Only each host exact runtime-root prefix becomes ${HIL_RUNTIME}; raw hashes retained; binary assets never normalized',
        profiles=[dict(profile=p['id'], runtime=p['runtime']) for p in catalog()['profiles']],
        pc=local, x5=remote, normalized_path_files=normalized_paths, differences=differences)
    args.output.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps({k: v for k, v in report.items() if k not in ('pc', 'x5')}, indent=2))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
