import json
from pathlib import Path
import sys

import pytest

sys.path.insert(0, str(Path(__file__).parents[1]/'hil'))
from agent import Agent
import agent as agent_module
from backend import RuntimeBackend
from catalog import ROOT, profile


def test_remote_agent_cannot_target_the_real_workspace(tmp_path):
    with pytest.raises(ValueError):
        Agent(Path.home()/'agribot_ws/ros2_ws', 'rdk')


def test_missing_independent_navigation_plugin_is_rejected_before_hardware(tmp_path, monkeypatch):
    from types import SimpleNamespace
    for relative in ('underlay.bash', 'install/setup.bash', 'runtime/tractor/rdk/mission.json',
                     'runtime/tractor/world.sdf'):
        path = tmp_path/relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.touch()
    instance = SimpleNamespace(root=tmp_path, role='pc', settings={'underlay': str(tmp_path/'underlay.bash')},
        select=lambda _: dict(car='Car4', runtime='tractor'))
    monkeypatch.setattr(agent_module, 'execute', lambda *_a, **_k: pytest.fail('Hardware must not be contacted'))
    with pytest.raises(RuntimeError, match='MPPI compatibility build is missing'):
        Agent.preflight(instance, 'tractor_cvt')


@pytest.mark.parametrize('identifier', ['ackermann_apple', 'tracked_sprayer', 'tracked_shredder',
                                      'tractor_powershift', 'tractor_cvt'])
@pytest.mark.parametrize('available', [False, True])
def test_visual_localization_checks_camera_codec_before_hardware_start(tmp_path, monkeypatch, identifier, available):
    from types import SimpleNamespace
    underlay = tmp_path/'underlay.bash'
    underlay.touch()
    (tmp_path/'agent.json').write_text(json.dumps({'source_root': str(ROOT), 'underlay': str(underlay)}))
    (tmp_path/'catalog.json').write_text((ROOT/'unity/Assets/AgribotConfig/Resources/hil_profiles.json').read_text())
    selected = profile(identifier)
    scene = tmp_path/'runtime'/selected['runtime']
    (scene/'rdk').mkdir(parents=True)
    (scene/'rdk/mission.json').write_text('{}')
    (scene/'world.sdf').touch()
    (tmp_path/'install').mkdir()
    (tmp_path/'install/setup.bash').touch()
    plugin = tmp_path/'install/nav2_mppi_controller/share/nav2_mppi_controller/mppic.xml'
    plugin.parent.mkdir(parents=True)
    plugin.touch()
    calls = []

    def dependency(command, check):
        calls.append(command)
        return SimpleNamespace(returncode=0 if available else 1)

    monkeypatch.setattr(agent_module, 'execute', dependency)
    instance = Agent(tmp_path, 'pc')
    needs_camera = selected['car'] in ('Car0', 'Car3')
    if needs_camera and not available:
        with pytest.raises(RuntimeError, match='ros-humble-compressed-image-transport'):
            instance.preflight(identifier)
    else:
        assert instance.preflight(identifier)['ready'] is True
    assert len(calls) == int(needs_camera)
    if needs_camera:
        assert 'compressed_image_transport' in calls[0][-1]


@pytest.mark.parametrize('identifier', ['ackermann_apple', 'tracked_sprayer', 'tracked_shredder',
                                     'tractor_powershift', 'tractor_cvt'])
def test_agent_commands_use_only_the_selected_isolated_scenario(tmp_path, identifier):
    (tmp_path/'agent.json').write_text(json.dumps({'source_root': str(ROOT), 'underlay': '/opt/ros/humble/setup.bash'}))
    (tmp_path/'catalog.json').write_text((ROOT/'unity/Assets/AgribotConfig/Resources/hil_profiles.json').read_text())
    agent = Agent(tmp_path, 'pc')
    selected = profile(identifier)
    env = agent.environment(selected)
    assert env['AGRIBOT_HIL_SCENE_ROOT'] == str(tmp_path/'runtime'/selected['runtime'])
    assert env['ROS_DOMAIN_ID'] in ('73', '74')
    assert '/agribot_ws/' not in json.dumps(agent.command(selected, 'test_run', 'runtime'), default=str)


def test_legacy_bridges_are_authorized_only_after_readiness_before_mission(tmp_path, monkeypatch):
    backend = RuntimeBackend({}, tmp_path, lambda *_: None)
    calls = []
    monkeypatch.setattr(backend, 'ensure_pc_runtime', lambda: None)
    monkeypatch.setattr(backend, 'agent', lambda role, action, *args: calls.append((role, action)))
    backend.start_mission(profile('tracked_sprayer'))
    assert calls == [('pc', 'authorize'), ('rdk', 'authorize'), ('rdk', 'mission')]
    calls.clear()
    backend.start_mission(profile('tractor_cvt'))
    assert calls == [('rdk', 'mission')]


@pytest.mark.parametrize('identifier', ['ackermann_apple', 'tracked_sprayer', 'tracked_shredder',
                                      'tractor_powershift', 'tractor_cvt'])
def test_dead_pc_runtime_blocks_authorization_and_mission(tmp_path, monkeypatch, identifier):
    from types import SimpleNamespace
    backend = RuntimeBackend({}, tmp_path, lambda *_: None)
    monkeypatch.setattr(backend, 'execute', lambda *_a, **_k:
                        SimpleNamespace(returncode=3, stdout='inactive\n'))
    monkeypatch.setattr(backend, 'agent', lambda *_a, **_k: pytest.fail('Dead PC must not authorize or start'))
    with pytest.raises(RuntimeError, match='物理CAN反馈进程已退出'):
        backend.start_mission(profile(identifier))


def test_monitor_detects_pc_exit_before_polling_x5(tmp_path, monkeypatch):
    from types import SimpleNamespace
    backend = RuntimeBackend({}, tmp_path, lambda *_: None)
    monkeypatch.setattr(backend, 'execute', lambda *_a, **_k:
                        SimpleNamespace(returncode=3, stdout='failed\n'))
    monkeypatch.setattr(backend, 'agent', lambda *_a, **_k: pytest.fail('PC exit must be reported directly'))
    with pytest.raises(RuntimeError, match='物理CAN反馈进程已退出'):
        backend.monitor(profile('tractor_cvt'))


def test_confirmed_stop_is_idempotent_but_failed_stop_can_be_retried(tmp_path, monkeypatch):
    backend = RuntimeBackend({}, tmp_path, lambda *_: None)
    calls = []
    monkeypatch.setattr(backend, 'agent', lambda role, action, **kwargs: calls.append((role, action)))
    backend.safe_stop()
    backend.safe_stop()
    assert calls == [('rdk', 'stop'), ('pc', 'stop')]
    backend.stop_confirmed = False
    calls.clear()
    def failed(role, action, **kwargs):
        calls.append((role, action))
        if role == 'rdk':
            raise RuntimeError('transport unavailable')
    monkeypatch.setattr(backend, 'agent', failed)
    with pytest.raises(RuntimeError, match='清理未完全确认'):
        backend.safe_stop()
    assert backend.stop_confirmed is False
    monkeypatch.setattr(backend, 'agent', lambda role, action, **kwargs: calls.append((role, action)))
    backend.safe_stop()
    assert backend.stop_confirmed is True
    assert calls == [('rdk', 'stop'), ('pc', 'stop')]*2


@pytest.mark.parametrize('unit_state,expected', [('active', 'running'), ('inactive', 'failed')])
def test_partial_mission_report_never_cancels_a_live_task_or_becomes_a_pass(tmp_path, monkeypatch, unit_state, expected):
    from types import SimpleNamespace
    directory = tmp_path/'runs/test_run/mission'
    directory.mkdir(parents=True)
    (directory/'mission.json').write_text('')
    instance = SimpleNamespace(root=tmp_path, select=lambda _: {'car': 'Car0'},
                               unit=lambda _: 'test-mission.service')
    monkeypatch.setattr(agent_module, 'execute', lambda *args, **kwargs: SimpleNamespace(stdout=unit_state))
    result = Agent.mission_status(instance, 'ackermann_apple', 'test_run')
    assert result['status'] == expected
    assert 'result' not in result
    if expected == 'running': assert result['report_pending'] is True
