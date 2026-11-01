"""The native UI must not require a proprietary physical-camera SDK to simulate."""
from pathlib import Path
import os
import subprocess

import pytest


def test_ui_incremental_build_excludes_the_same_camera_driver_as_bootstrap():
    root = Path(__file__).resolve().parents[2]
    wrapper = root / 'scripts/build_ros.sh'
    handoff = root / 'ros2_ws/src/agribot_vehicle_description/scripts/unity_sync_and_simulate.sh'
    for script in (wrapper, handoff):
        subprocess.run(['bash', '-n', str(script)], check=True)
        assert '--packages-ignore hikrobot_mvs_ros2' in script.read_text(), str(script)


@pytest.mark.parametrize(('overrides', 'expected'), [
    ({}, ('2', '-j2')),
    ({'AGRIBOT_BUILD_JOBS_PER_PACKAGE': '3'}, ('3', '-j3')),
    ({'CMAKE_BUILD_PARALLEL_LEVEL': '4'}, ('4', '-j4')),
    ({'CMAKE_BUILD_PARALLEL_LEVEL': '4', 'MAKEFLAGS': '-j1 -l1'}, ('4', '-j1 -l1')),
])
def test_make_receives_the_build_limit_without_overriding_explicit_flags(overrides, expected):
    script = Path(__file__).resolve().parents[2] / 'ros2_ws/build_ros2_humble.sh'
    assignments = [line for line in script.read_text().splitlines()
                   if line.startswith(('export CMAKE_BUILD_PARALLEL_LEVEL=', 'export MAKEFLAGS='))]
    assert len(assignments) == 2
    env = dict(os.environ)
    for name in ('CMAKE_BUILD_PARALLEL_LEVEL', 'AGRIBOT_BUILD_JOBS_PER_PACKAGE', 'MAKEFLAGS'):
        env.pop(name, None)
    env.update(overrides)
    command = 'default_build_jobs=2\n' + '\n'.join(assignments)
    command += '\nprintf "%s\\n" "$CMAKE_BUILD_PARALLEL_LEVEL" "$MAKEFLAGS"'
    result = subprocess.run(['bash', '-c', command], env=env, capture_output=True, text=True, check=True)
    assert tuple(result.stdout.splitlines()) == expected
