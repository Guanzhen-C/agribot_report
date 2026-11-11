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
    monkeypatch.setattr(backend, 'agent', lambda role, action, *args: calls.append((role, action)))
    backend.start_mission(profile('tracked_sprayer'))
    assert calls == [('pc', 'authorize'), ('rdk', 'authorize'), ('rdk', 'mission')]
    calls.clear()
    backend.start_mission(profile('tractor_cvt'))
    assert calls == [('rdk', 'mission')]


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
