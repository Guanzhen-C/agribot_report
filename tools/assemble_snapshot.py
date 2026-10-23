#!/usr/bin/env python3
"""Export current source worktrees without copying their Git history or caches."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from datetime import datetime, timezone


DENIED_PARTS = {'.git', '.codex', '.claude', '.vscode', '.idea', '__pycache__',
                '.pytest_cache', 'node_modules', 'Library', 'Temp', 'Logs',
                'UserSettings', '.gradle', '.board_sync'}
DENIED_FILES = {'AGENTS.md', 'CLAUDE.md', '.env', 'local.properties'}
DENIED_SUFFIXES = {'.db3', '.bag', '.pyc', '.pyo'}
PRIVATE_PREFIXES = ('ros2_ws/docs/patent/', 'ros2_ws/docs/software_copyright/')
UNITY_ASSETS = (
    'Assets/AgribotConfig/Resources/LocalFarmAssets',
    'Assets/AgribotConfig/Resources/LocalGarageThumbnails',
    'Assets/AgribotConfig/Resources/LocalGarageVehicles',
    'Assets/AgribotConfig/Resources/LocalVehicleAppearances',
    'Assets/StreamingAssets/LocalFarmExports',
    'Assets/StreamingAssets/LocalVehicleAppearances',
)
ROS_ASSETS = ('ros2_ws/src/agribot_vehicle_description/generated/',)


def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args])


def permitted(path):
    return not (set(path.parts) & DENIED_PARTS or path.name in DENIED_FILES
                or path.suffix in DENIED_SUFFIXES or str(path).startswith(PRIVATE_PREFIXES)
                or path.name.startswith('.env.') and path.name != '.env.example')


def selected_files(root, role, include_local_assets):
    names = set(git(root, 'ls-files', '-z', '--cached', '--others', '--exclude-standard').decode().split('\0'))
    names.discard('')
    if role == 'ros':
        scope = json.loads((Path(__file__).resolve().parents[1] / 'manifests/delivery_scope.json').read_text())
        names = {name for name in names if name in scope['ros_root_files'] or
                 (len(Path(name).parts) >= 3 and Path(name).parts[:2] == ('ros2_ws', 'src')
                  and Path(name).parts[2] in scope['ros_source_roots'])}
    if include_local_assets:
        prefixes = UNITY_ASSETS if role == 'unity' else ROS_ASSETS if role == 'ros' else ()
        for prefix in prefixes:
            folder = root / prefix
            if folder.is_dir():
                names.update(str(p.relative_to(root)) for p in folder.rglob('*') if p.is_file())
            if Path(str(folder) + '.meta').is_file():
                names.add(prefix + '.meta')
    return [Path(name) for name in sorted(names) if permitted(Path(name)) and (root / name).is_file()]


def destination(role, relative):
    if role == 'unity':
        return Path('unity') / relative
    if relative == Path('README.md'):
        return Path('docs/legacy/ROS_README.md')
    if relative == Path('.gitignore'):
        return Path('docs/legacy/ros.gitignore.txt')
    return relative


def assemble(root, sources, include_local_assets):
    manifest = {'schema_version': 1, 'created_utc': datetime.now(timezone.utc).isoformat(),
                'includes_authorized_local_assets': include_local_assets, 'sources': [], 'files': []}
    for role, source in sources.items():
        source = source.resolve()
        record = {'role': role, 'head': git(source, 'rev-parse', 'HEAD').decode().strip(),
                  'branch': git(source, 'branch', '--show-current').decode().strip(),
                  'has_uncommitted_changes': bool(git(source, 'status', '--porcelain')), 'files': 0, 'bytes': 0}
        for relative in selected_files(source, role, include_local_assets):
            origin = source / relative
            if origin.is_symlink() and not origin.resolve().is_relative_to(source):
                raise ValueError('External symlink needs explicit packaging: ' + str(relative))
            target_relative = destination(role, relative)
            target = root / target_relative
            if target_relative.parts[0] in {'tools', 'scripts', '.github'} and role == 'ros':
                raise ValueError('Unexpected source root path: ' + str(relative))
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(origin.resolve(), target)
            with target.open('rb') as stream:
                beginning = stream.read(128)
                if beginning.startswith(b'version https://git-lfs.github.com/spec/v1'):
                    raise ValueError('Missing LFS object; run git lfs pull in the source: ' + str(relative))
                digest = hashlib.sha256(beginning)
                for chunk in iter(lambda: stream.read(4 * 1024**2), b''):
                    digest.update(chunk)
            size = target.stat().st_size
            manifest['files'].append({'path': str(target_relative), 'bytes': size,
                                      'sha256': digest.hexdigest(), 'source': role})
            record['files'] += 1
            record['bytes'] += size
        manifest['sources'].append(record)
    output = root / 'manifests'
    output.mkdir(exist_ok=True)
    (output / 'source_snapshot.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(manifest['sources'], ensure_ascii=False, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ros', type=Path, required=True)
    parser.add_argument('--unity', type=Path, required=True)
    parser.add_argument('--include-authorized-local-assets', action='store_true')
    args = parser.parse_args()
    assemble(Path(__file__).resolve().parents[1],
             {'ros': args.ros, 'unity': args.unity},
             args.include_authorized_local_assets)


if __name__ == '__main__':
    main()
