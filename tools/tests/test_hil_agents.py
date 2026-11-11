import json
from pathlib import Path
import sys

import pytest

sys.path.insert(0, str(Path(__file__).parents[1]/'hil'))
from agent import Agent
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
