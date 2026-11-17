"""Select the app-owned acceleration-fixed drive plugin for Car3 HIL only."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

LIBRARY = 'libagribot_hil_diff_drive.so'


def configure(runtime):
    runtime = Path(runtime)
    report = dict(scope='Car3 PC simulation only; no geometry, contacts, navigation, firmware or real CAN changes', models={})
    prepared = []
    for name in ('tracked_sprayer', 'tracked_shredder'):
        path = runtime/name/'vehicle.sdf'
        before = path.read_bytes()
        tree = ET.fromstring(before)
        plugins = tree.findall(".//plugin[@name='configured_diff_drive']")
        if len(plugins) != 1 or plugins[0].get('filename') not in ('libgazebo_ros_diff_drive.so', LIBRARY):
            raise ValueError('Unexpected Car3 drive plugin: '+name)
        prepared.append((name, path, before, tree, plugins[0]))
    for name, path, before, tree, plugin in prepared:
        if plugin.get('filename') != LIBRARY:
            plugin.set('filename', LIBRARY)
            ET.indent(tree, space='  ')
            path.write_text(ET.tostring(tree, encoding='unicode'))
        report['models'][name] = dict(before_sha256=hashlib.sha256(before).hexdigest(),
            after_sha256=hashlib.sha256(path.read_bytes()).hexdigest(), library=LIBRARY)
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = configure(args.runtime)
    if args.output:
        args.output.write_text(json.dumps(result, indent=2)+'\n')
    print(json.dumps(result, indent=2))
