#!/usr/bin/env python3
"""Verify delivered source against original snapshot and reviewed path fixes."""
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def main():
    manifest = json.loads((ROOT / 'manifests/source_snapshot.json').read_text())
    patches = {row['path']: row['delivery_sha256'] for row in
               json.loads((ROOT / 'manifests/portability_changes.json').read_text())}
    missing, changed, excluded = [], [], []
    for record in manifest['files']:
        path = ROOT / record['path']
        if not path.is_file():
            if path.suffix in {'.apk', '.aab', '.pcd', '.pyc', '.db3', '.bag'}:
                excluded.append(record['path'])
            else:
                missing.append(record['path'])
            continue
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if digest != patches.get(record['path'], record['sha256']):
            changed.append(record['path'])
    result = {'checked': len(manifest['files']), 'intentionally_excluded_artifacts': excluded,
              'unexpected_missing': missing, 'unexpected_changed': changed}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return bool(missing or changed)


if __name__ == '__main__':
    sys.exit(main())
