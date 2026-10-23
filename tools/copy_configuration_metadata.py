#!/usr/bin/env python3
"""Copy non-mesh vehicle contracts so the public algorithm catalog is complete.

This does not grant or copy the excluded CAD meshes/preview images.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source_generated', type=Path)
    args = parser.parse_args()
    records = []
    for path in sorted(args.source_generated.glob('*/vehicle_config.json')):
        target = ROOT / 'ros2_ws/src/agribot_vehicle_description/generated' / path.parent.name / path.name
        if target.exists():
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
        records.append({'path': str(target.relative_to(ROOT)),
                        'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                        'note': 'Configuration only; referenced meshes are not bundled.'})
    (ROOT / 'manifests/additional_configuration_metadata.json').write_text(
        json.dumps(records, indent=2, ensure_ascii=False) + '\n')
    print('Added configuration contracts:', len(records))


if __name__ == '__main__':
    main()
