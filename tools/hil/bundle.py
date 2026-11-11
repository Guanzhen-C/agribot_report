"""Import verified HIL scenes into a relocatable, hashed runtime bundle."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET

import yaml

MEDIA = {'.obj', '.mtl', '.dae', '.stl', '.png', '.jpg', '.jpeg', '.dds', '.tga', '.material'}
TEXT = {'.sdf', '.urdf', '.xml', '.json', '.yaml', '.yml'}


class Importer:
    def __init__(self, seeds, output):
        self.seeds, self.output = Path(seeds).resolve(), Path(output).resolve()
        self.asset_dirs = {}
        self.aliases = {
            '/home/cgz/.cache/agribot/hil_apple/': 'ackermann_apple/',
            '/home/sunrise/agribot_hil_ws/scenario/': 'ackermann_apple/rdk/',
            '/home/cgz/.cache/agribot/isobus/tracked_orchard/': 'tracked_sprayer/',
            '/home/sunrise/agribot_isobus_hil_ws/scenario_orchard/': 'tracked_sprayer/rdk/',
            '/home/cgz/.cache/agribot/isobus/tracked_shredder_orchard/': 'tracked_shredder/',
            '/home/sunrise/agribot_isobus_hil_ws/scenario_shredder/': 'tracked_shredder/rdk/',
            '/home/cgz/.local/state/agribot-harrow-visual-20261111/scenario-two-stage-01/': 'tractor_harrow/',
            '/home/sunrise/agribot_hitch_hil_ws/scenario/': 'tractor_harrow/rdk/',
        }

    def location(self, value):
        if not isinstance(value, str):
            return value
        scheme = 'file://' if value.startswith('file://') else ''
        original = value[len(scheme):]
        for prefix, target in self.aliases.items():
            if original.startswith(prefix):
                candidate = self.seeds/target/original[len(prefix):]
                if candidate.exists():
                    return scheme+'@HIL_ROOT@/'+target+original[len(prefix):]
        path = Path(original)
        if not path.is_absolute() or not path.is_file():
            return value
        if path.suffix.lower() not in MEDIA:
            return value
        directory = path.parent.resolve()
        if directory not in self.asset_dirs:
            key = hashlib.sha256(str(directory).encode()).hexdigest()[:12]
            target = self.output/'shared_assets'/key
            self.asset_dirs[directory] = target
            for item in directory.rglob('*'):
                if item.is_file() and item.suffix.lower() in MEDIA:
                    destination = target/item.relative_to(directory)
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(item, destination)
        target = self.asset_dirs[directory]/path.name
        return scheme+'@HIL_ROOT@/'+target.relative_to(self.output).as_posix()

    def structured(self, value):
        if isinstance(value, list):
            return [self.structured(item) for item in value]
        if isinstance(value, dict):
            return {key: self.structured(item) for key, item in value.items()}
        if isinstance(value, str) and value.lstrip().startswith('<robot'):
            robot = ET.fromstring(value)
            self.xml_paths(robot)
            return ET.tostring(robot, encoding='unicode')
        return self.location(value)

    def xml_paths(self, root):
        for element in root.iter():
            if element.text:
                element.text = self.location(element.text.strip())
            for key, value in list(element.attrib.items()):
                element.set(key, self.location(value))

    def run(self):
        if self.output.exists():
            raise FileExistsError(self.output)
        self.output.mkdir(parents=True)
        for source in self.seeds.rglob('*'):
            if not source.is_file():
                continue
            target = self.output/source.relative_to(self.seeds)
            target.parent.mkdir(parents=True, exist_ok=True)
            if source.suffix in ('.sdf', '.urdf', '.xml'):
                tree = ET.parse(source)
                self.xml_paths(tree.getroot())
                ET.indent(tree, space='  ')
                tree.write(target, encoding='utf-8', xml_declaration=True)
            elif source.suffix in ('.yaml', '.yml'):
                target.write_text(yaml.safe_dump(self.structured(yaml.safe_load(source.read_text())), sort_keys=False))
            elif source.suffix == '.json':
                target.write_text(json.dumps(self.structured(json.loads(source.read_text())), indent=2)+'\n')
            else:
                shutil.copy2(source, target)
        hashes = {path.relative_to(self.output).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                  for path in sorted(self.output.rglob('*')) if path.is_file()}
        report = {'schema_version': 1, 'token': '@HIL_ROOT@', 'files': hashes,
                  'scope': 'Verified HIL scenes; no real X5 workspace or logs'}
        (self.output/'bundle.json').write_text(json.dumps(report, indent=2)+'\n')
        return report


def materialize(source, destination, refresh=False):
    source, destination = Path(source).resolve(), Path(destination).resolve()
    manifest = json.loads((source/'bundle.json').read_text())
    if destination.exists() and not refresh:
        raise FileExistsError(destination)
    for relative, expected in manifest['files'].items():
        path = source/relative
        if not path.resolve().is_relative_to(source) or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            raise ValueError('HIL bundle integrity mismatch: '+relative)
    destination.mkdir(parents=True, exist_ok=refresh)
    for relative in manifest['files']:
        target, original = destination/relative, source/relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if original.suffix in ('.sdf', '.urdf'):
            # gazebo_ros spawn_entity passes Unicode to lxml, which rejects an
            # encoding declaration. Preserve the XML tree without that header.
            root = ET.fromstring(original.read_text().replace('@HIL_ROOT@', str(destination)))
            target.write_text(ET.tostring(root, encoding='unicode'))
        elif original.suffix in TEXT:
            # The token occurs only in path strings written by the structured importer.
            target.write_text(original.read_text().replace('@HIL_ROOT@', str(destination)))
        elif not refresh or not target.is_file():
            shutil.copy2(original, target)
    return destination


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--seeds', type=Path)
    parser.add_argument('--bundle', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.seeds and not args.bundle:
        value = Importer(args.seeds, args.output).run()
        print(json.dumps({'output': str(args.output), 'files': len(value['files'])}))
    elif args.bundle and not args.seeds:
        print(materialize(args.bundle, args.output))
    else:
        parser.error('Choose --seeds or --bundle')
