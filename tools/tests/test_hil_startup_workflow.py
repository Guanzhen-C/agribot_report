import sys
from pathlib import Path
import pytest

sys.path.insert(0, str(Path(__file__).parents[1]/'hil'))
from catalog import flash_segments, firmware, load, profile
from workflow import Startup


class Backend:
    def __init__(self, fail=None):
        self.events, self.fail = [], fail

    def __getattr__(self, name):
        def call(*args):
            self.events.append(name)
            if name == self.fail:
                raise RuntimeError('injected '+name)
        return call


def test_only_reviewed_combinations_and_firmware_assets():
    value = load()
    assert len(value['profiles']) == 5
    assert {item['car'] for item in value['profiles']} == {'Car0', 'Car3', 'Car4'}
    for image in value['firmwares']:
        assert firmware(image['id'])['segments']
    assert profile('Car9') is None
    assert profile('tractor_cvt')['transmission'] == 'cvt_auto'


def test_every_start_flashes_even_if_the_same_profile_is_repeated():
    backend, stages = Backend(), []
    runner = Startup(backend, stages.append)
    runner.run('tractor_cvt')
    runner.run('tractor_cvt')
    assert backend.events.count('flash') == 2
    assert backend.events[:8] == ['preflight', 'stop_previous', 'verify_real_runtime', 'flash',
        'configure_bus', 'start_pc', 'start_x5', 'wait_ready']
    assert backend.events[-2:] == ['verify_real_runtime', 'start_mission']


@pytest.mark.parametrize('failure', ['preflight', 'stop_previous', 'verify_real_runtime', 'flash',
    'configure_bus', 'start_pc', 'start_x5', 'wait_ready'])
def test_failure_never_starts_mission_and_attempts_safe_stop(failure):
    backend, stages = Backend(failure), []
    with pytest.raises(RuntimeError):
        Startup(backend, stages.append).run('tracked_sprayer')
    assert 'start_mission' not in backend.events
    assert backend.events[-1] == 'safe_stop'
    assert stages[-1] == 'failed'


def test_unknown_profile_has_no_hardware_side_effect():
    backend = Backend()
    with pytest.raises(ValueError):
        Startup(backend, lambda _: None).run('tractor_cvt; rm')
    assert backend.events == []


def test_cancel_during_startup_cannot_authorize_motion():
    backend, stages = Backend(), []
    with pytest.raises(InterruptedError):
        Startup(backend, stages.append, lambda: 'flashing' in stages).run('tractor_cvt')
    assert 'start_mission' not in backend.events
    assert backend.events[-1] == 'safe_stop'


def test_arbitrary_file_is_not_flashable():
    with pytest.raises(ValueError):
        flash_segments(b'not a board application')
