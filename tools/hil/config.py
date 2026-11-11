"""Local-only HIL connection settings, never a real-vehicle deployment target."""
import json
import os
from pathlib import Path
import re
import shlex

from catalog import ROOT


def load(path=None):
    path = Path(path or os.environ.get('AGRIBOT_HIL_CONFIG', ROOT/'.local/hil/connection.json'))
    value = json.loads(path.read_text())
    if not re.fullmatch(r'[a-z_][a-z0-9_-]*@[a-zA-Z0-9][a-zA-Z0-9.-]*', value['host']):
        raise ValueError('Invalid HIL SSH target')
    remote = Path(value['remote_root'])
    if not remote.is_absolute() or remote.name != 'agribot_app_hil_ws' or str(remote).startswith('/home/sunrise/agribot_ws/'):
        raise ValueError('X5 writes must target only the separate agribot_app_hil_ws')
    for key in ('identity', 'known_hosts', 'openocd', 'openocd_scripts'):
        value[key] = str(Path(value[key]).expanduser().resolve())
        if not Path(value[key]).exists():
            raise FileNotFoundError(value[key])
    if not re.fullmatch('[a-zA-Z0-9]+', value['probe_serial']):
        raise ValueError('Invalid debug probe identity')
    return value


def ssh_arguments(config, *command):
    return ['ssh', '-i', config['identity'], '-o', 'IdentitiesOnly=yes', '-o', 'BatchMode=yes',
            '-o', 'StrictHostKeyChecking=yes', '-o', 'ConnectTimeout=8',
            '-o', 'UserKnownHostsFile='+config['known_hosts'], config['host'], shlex.join(command)]
