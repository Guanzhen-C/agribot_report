#!/usr/bin/env python3
"""Archive out-of-scope files from this delivery copy, never the source worktrees."""
import argparse
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True)
    args = parser.parse_args()
    archive = args.archive.resolve()
    if archive == ROOT or ROOT in archive.parents or archive.exists():
        raise ValueError('Archive must be a new directory outside this repository')
    scope = json.loads((ROOT / 'manifests/delivery_scope.json').read_text())
    allowed = set(scope['ros_source_roots'])
    moves = [ROOT / 'firmware', ROOT / 'calibration', ROOT / 'docs/legacy']
    moves += [path for path in (ROOT / 'ros2_ws/src').iterdir()
              if path.name not in allowed]
    moves += [path for path in (ROOT / 'ros2_ws').iterdir()
              if path.name not in {'src', 'build', 'install', 'log', 'build_ros2_humble.sh'}]
    for path in moves:
        if not path.exists():
            continue
        target = archive / path.relative_to(ROOT)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(str(path), target)
    manifest_path = ROOT / 'manifests/source_snapshot.json'
    manifest = json.loads(manifest_path.read_text())
    manifest['files'] = [row for row in manifest['files'] if (ROOT / row['path']).is_file()]
    manifest['sources'] = [row for row in manifest['sources'] if row['role'] != 'firmware']
    manifest['selection_scope'] = scope['product']
    for source in manifest['sources']:
        rows = [row for row in manifest['files'] if row['source'] == source['role']]
        source['files'] = len(rows)
        source['bytes'] = sum(row['bytes'] for row in rows)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    print('Archived out-of-scope content outside the delivery repository.')


if __name__ == '__main__':
    main()
