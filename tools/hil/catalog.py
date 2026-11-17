"""Shared Unity/Python catalog for the explicitly verified HIL combinations."""
import hashlib
import json
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[2]
CATALOG = ROOT/'unity/Assets/AgribotConfig/Resources/hil_profiles.json'


def load(root=ROOT):
    value = json.loads((Path(root)/CATALOG.relative_to(ROOT)).read_text())
    if value.get('schemaVersion') != 1:
        raise ValueError('Unsupported HIL catalog schema')
    ids = set()
    for profile in value['profiles']:
        if not re.fullmatch('[a-z][a-z0-9_]*', profile['id']) or profile['id'] in ids:
            raise ValueError('Invalid or duplicate HIL profile')
        ids.add(profile['id'])
        if profile['domain'] not in (73, 74) or profile['car'] not in ('Car0', 'Car3', 'Car4'):
            raise ValueError('HIL must stay in its dedicated domain and vehicle scope')
        if profile['firmware'] not in {f['id'] for f in value['firmwares']}:
            raise ValueError('Missing HIL firmware binding')
    return value


def profile(identifier, root=ROOT):
    return next((item for item in load(root)['profiles'] if item['id'] == identifier), None)


def flash_segments(data):
    if data[:7] != b'\x7fELF\x01\x01\x01' or struct.unpack_from('<H', data, 18)[0] != 243:
        raise ValueError('Expected a 32-bit little-endian RISC-V ELF')
    offset = struct.unpack_from('<I', data, 28)[0]
    width, count = struct.unpack_from('<HH', data, 42)
    if width != 32 or count > 64:
        raise ValueError('Invalid program header table')
    result = []
    for index in range(count):
        kind, position, _, address, size, _, _, _ = struct.unpack_from('<8I', data, offset+index*width)
        if kind != 1 or not size:
            continue
        # Only application flash segments, never the persistent configuration sectors.
        if address < 0x80000400 or address+size > 0x80FF0000 or position+size > len(data):
            raise ValueError('ELF writes outside the HIL application flash region')
        result.append({'address': address, 'size': size, 'offset': position})
    if not result:
        raise ValueError('ELF contains no flash application')
    return result


def firmware(identifier, root=ROOT):
    root = Path(root).resolve()
    item = next((value for value in load(root)['firmwares'] if value['id'] == identifier), None)
    if item is None:
        raise ValueError('Unknown HIL firmware')
    path = (root/item['file']).resolve()
    if not path.is_relative_to(root/'resources/hil/firmware'):
        raise ValueError('Firmware path escaped the reviewed directory')
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != item['sha256']:
        raise ValueError('HIL firmware hash mismatch')
    return dict(item, path=str(path), segments=flash_segments(data))
