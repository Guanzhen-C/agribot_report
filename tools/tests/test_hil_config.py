import json
import shutil
from pathlib import Path
import sys

import pytest

sys.path.insert(0, str(Path(__file__).parents[1]/'hil'))
import config


def fixture(tmp_path):
    file = tmp_path/'fixture'
    file.write_text('fixture')
    return {'host': 'sunrise@192.168.1.102', 'remote_root': '/home/sunrise/agribot_app_hil_ws',
            'identity': str(file), 'known_hosts': str(file), 'openocd': str(file),
            'openocd_scripts': str(tmp_path), 'probe_serial': '1234abc'}


@pytest.mark.parametrize('root', ['/home/sunrise/agribot_ws', '/home/sunrise/agribot_ws/ros2_ws', '/tmp/shared', '../agribot_app_hil_ws'])
def test_never_deploy_into_real_vehicle_workspace(tmp_path, root):
    value = fixture(tmp_path)
    value['remote_root'] = root
    file = tmp_path/'config.json'
    file.write_text(json.dumps(value))
    with pytest.raises(ValueError):
        config.load(file)


def test_ssh_target_cannot_inject_options_or_shell(tmp_path):
    value = fixture(tmp_path)
    file = tmp_path/'config.json'
    for host in ('-oProxyCommand=bad', 'sunrise@host;reboot', 'root@host && bad'):
        value['host'] = host
        file.write_text(json.dumps(value))
        with pytest.raises(ValueError):
            config.load(file)


def test_dedicated_hil_workspace_is_allowed(tmp_path):
    value = fixture(tmp_path)
    file = tmp_path/'config.json'
    file.write_text(json.dumps(value))
    loaded = config.load(file)
    args = config.ssh_arguments(loaded, 'python3', '/a path/agent.py', 'status')
    assert args[-1] == "python3 '/a path/agent.py' status"
    assert 'BatchMode=yes' in args


def test_flash_scripts_are_pinned_and_tampering_is_rejected(tmp_path):
    original = config.ROOT/'resources/hil/openocd'
    config.verify_flash_scripts({'openocd_scripts': str(original)})
    copy = tmp_path/'openocd'
    shutil.copytree(original, copy)
    config.verify_flash_scripts({'openocd_scripts': str(copy)})
    (copy/'soc/hpm_reset.cfg').write_text('modified reset')
    with pytest.raises(ValueError, match='已验证版本不一致'):
        config.verify_flash_scripts({'openocd_scripts': str(copy)})
