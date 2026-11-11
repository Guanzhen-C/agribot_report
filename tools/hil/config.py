"""Local-only HIL connection settings, never a real-vehicle deployment target."""
import json
import hashlib
import os
from pathlib import Path
import re
import shlex

from catalog import ROOT


def load(path=None, validate_flash=True):
    path = Path(path or os.environ.get('AGRIBOT_HIL_CONFIG', ROOT/'.local/hil/connection.json'))
    value = json.loads(path.read_text())
    value.setdefault('openocd_scripts', str(ROOT/'resources/hil/openocd'))
    if not re.fullmatch(r'[a-z_][a-z0-9_-]*@[a-zA-Z0-9][a-zA-Z0-9.-]*', value['host']):
        raise ValueError('Invalid HIL SSH target')
    remote = Path(value['remote_root'])
    if not remote.is_absolute() or remote.name != 'agribot_app_hil_ws' or str(remote).startswith('/home/sunrise/agribot_ws/'):
        raise ValueError('X5 writes must target only the separate agribot_app_hil_ws')
    keys = ('identity', 'known_hosts', 'openocd', 'openocd_scripts') if validate_flash else ('identity', 'known_hosts')
    for key in keys:
        value[key] = str(Path(value[key]).expanduser().resolve())
        if not Path(value[key]).exists():
            raise FileNotFoundError(value[key])
    if not re.fullmatch('[a-zA-Z0-9]+', value['probe_serial']):
        raise ValueError('Invalid debug probe identity')
    return value


def verify_flash_scripts(config):
    manifest = json.loads((ROOT/'resources/hil/openocd/provenance.json').read_text())
    root = Path(config['openocd_scripts']).resolve()
    for relative, expected in manifest['files'].items():
        path = root/relative
        if not path.resolve().is_relative_to(root) or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
            raise ValueError('6750烧录配置与已验证版本不一致：'+relative)


def ssh_arguments(config, *command):
    return ['ssh', '-i', config['identity'], '-o', 'IdentitiesOnly=yes', '-o', 'BatchMode=yes',
            '-o', 'StrictHostKeyChecking=yes', '-o', 'ConnectTimeout=8',
            '-o', 'UserKnownHostsFile='+config['known_hosts'], config['host'], shlex.join(command)]
