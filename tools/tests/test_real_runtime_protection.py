import importlib.util
from pathlib import Path

MODULE = Path(__file__).parents[1]/'hil/protect_real_runtime.py'
spec = importlib.util.spec_from_file_location('protect_real_runtime', MODULE)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def test_runtime_trace_changes_remain_visible_but_do_not_mask_code_changes():
    log = 'agribot_ws/ros2_ws/src/FAST-LIVO2/Log/mat_pre.txt'
    source = 'agribot_ws/ros2_ws/src/FAST-LIVO2/src/main.cpp'
    before = {'files': {log: {'sha256': 'old'}, source: {'sha256': 'original'}}}
    after = {'files': {log: {'sha256': 'empty'}, source: {'sha256': 'original'}}}
    assert log in module.compare(before, after)
    assert module.compare(before, after, code_only=True) == {}
    after['files'][source] = {'sha256': 'changed'}
    assert source in module.compare(before, after, code_only=True)


def test_read_only_snapshot_detects_source_and_service_changes(tmp_path):
    source = tmp_path/'agribot_ws/ros2_ws/src'
    installed = tmp_path/'agribot_ws/ros2_ws/install'
    service = tmp_path/'.config/systemd/user'
    for folder in (source, installed, service):
        folder.mkdir(parents=True)
    path = source/'driver.py'
    path.write_text('original')
    (installed/'driver').symlink_to(path)
    (service/'real-car.service').write_text('ExecStart=original')
    before = module.snapshot(tmp_path)
    assert module.compare(before, module.snapshot(tmp_path)) == {}
    assert path.read_text() == 'original'
    path.write_text('changed')
    delta = module.compare(before, module.snapshot(tmp_path))
    assert list(delta) == ['agribot_ws/ros2_ws/src/driver.py']
    (service/'real-car.service').write_text('ExecStart=changed')
    assert len(module.compare(before, module.snapshot(tmp_path))) == 2


def test_hil_workspace_is_outside_protected_real_workspace(tmp_path):
    for directory in ('agribot_ws/ros2_ws/src', 'agribot_ws/ros2_ws/install', '.config/systemd/user'):
        (tmp_path/directory).mkdir(parents=True)
    (tmp_path/'agribot_ws/ros2_ws/src/driver.py').write_text('original')
    before = module.snapshot(tmp_path)
    separate = tmp_path/'agribot_app_hil_ws/src'
    separate.mkdir(parents=True)
    (separate/'hil.py').write_text('new isolated code')
    assert module.compare(before, module.snapshot(tmp_path)) == {}
