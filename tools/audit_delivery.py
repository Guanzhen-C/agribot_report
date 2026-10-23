#!/usr/bin/env python3
"""Audit publishable files without printing credential values."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SECRET = re.compile(rb'(?:sk-[A-Za-z0-9]{32,}|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,}|-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----)')


def main():
    names = subprocess.check_output(
        ['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=ROOT)
    paths = sorted(set(n.decode() for n in names.split(b'\0') if n))
    issues, packages, changes = [], [], []
    provenance = ROOT / 'manifests/source_snapshot.json'
    original = {f['path']: f for f in json.loads(provenance.read_text())['files']}
    scope = json.loads((ROOT / 'manifests/delivery_scope.json').read_text())
    for name in paths:
        path = ROOT / name
        if not path.is_file():
            continue
        parts = Path(name).parts
        if parts[0] == 'firmware' or (parts[:2] == ('ros2_ws', 'src')
                                    and len(parts) > 2 and parts[2] not in scope['ros_source_roots']):
            issues.append({'path': name, 'problem': 'outside the Unity/simulation delivery scope'})
        if path.stat().st_size > 100 * 1024**2:
            attr = subprocess.check_output(['git', 'check-attr', 'filter', '--', name], cwd=ROOT).decode()
            if not attr.strip().endswith(': lfs'):
                issues.append({'path': name, 'problem': 'GitHub large-file limit without LFS'})
        if path.stat().st_size < 8 * 1024**2:
            data = path.read_bytes()
            if b'\0' not in data:
                for match in SECRET.finditer(data):
                    issues.append({'path': name, 'line': data[:match.start()].count(b'\n') + 1,
                                   'problem': 'credential-like content (value withheld)'})
        if path.name == 'package.xml':
            try:
                tree = ET.parse(path).getroot()
                packages.append({'name': tree.findtext('name'), 'version': tree.findtext('version'),
                                 'license': [x.text for x in tree.findall('license')], 'path': name})
            except ET.ParseError:
                issues.append({'path': name, 'problem': 'invalid package.xml'})
        if name in original:
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            if digest != original[name]['sha256']:
                changes.append({'path': name, 'original_sha256': original[name]['sha256'],
                                'delivery_sha256': digest})
    (ROOT / 'manifests/ros_packages.json').write_text(json.dumps(packages, indent=2, ensure_ascii=False) + '\n')
    (ROOT / 'manifests/portability_changes.json').write_text(json.dumps(changes, indent=2, ensure_ascii=False) + '\n')
    result = {'files_scanned': len(paths), 'ros_packages': len(packages),
              'portability_changes': len(changes), 'issues': issues}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return bool(issues)


if __name__ == '__main__':
    sys.exit(main())
