import copy
import importlib.util
from pathlib import Path

import pytest

SOURCE = Path(__file__).parents[2]/'docs/deliverables/20261116_hil_retest/runtime_attestation.py'
spec = importlib.util.spec_from_file_location('hil_runtime_attestation', SOURCE)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def baseline():
    prefix = '/home/sunrise/agribot_app_hil_ws/install/nav2_mppi_controller/lib/'
    return dict(current=dict(run_id='fresh'), controllers=[dict(libraries=[
        dict(path=prefix+name) for name in ('libmppi_controller.so', 'libmppi_critics.so')])],
        python_modules=[dict(name=name, source_sha256='frozen', installed_sha256='frozen')
                        for name in ('mission.py', 'tracked_orchard_mission.py', 'async_trace.py')])


def test_actual_independent_libraries_accepted():
    module.validate(baseline(), 'fresh')


def test_tracked_runtime_requires_the_installed_isobus_adapter_attestation():
    value = baseline()
    value['current']['profile'] = 'tracked_sprayer'
    with pytest.raises(RuntimeError, match='incomplete'):
        module.validate(value, 'fresh')
    value['python_modules'].append(dict(name='isobus_work_client.py',
        source_sha256='fixed-adapter', installed_sha256='fixed-adapter'))
    module.validate(value, 'fresh')
    value['python_modules'][-1]['installed_sha256'] = 'old-adapter'
    with pytest.raises(RuntimeError, match='differs'):
        module.validate(value, 'fresh')


@pytest.mark.parametrize('change', ['run', 'system', 'missing', 'duplicate', 'stale-python'])
def test_incomplete_or_wrong_runtime_rejected(change):
    value = copy.deepcopy(baseline())
    if change == 'run':
        value['current']['run_id'] = 'old'
    elif change == 'system':
        value['controllers'][0]['libraries'][0]['path'] = '/opt/ros/humble/lib/libmppi_controller.so'
    elif change == 'missing':
        value['controllers'][0]['libraries'].pop()
    elif change == 'duplicate':
        value['controllers'].append(value['controllers'][0])
    else:
        value['python_modules'][0]['installed_sha256'] = 'old'
    with pytest.raises(RuntimeError):
        module.validate(value, 'fresh')
