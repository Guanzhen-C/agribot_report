"""The native UI must not require a proprietary physical-camera SDK to simulate."""
from pathlib import Path
import subprocess


def test_ui_incremental_build_excludes_the_same_camera_driver_as_bootstrap():
    root = Path(__file__).resolve().parents[2]
    wrapper = root / 'scripts/build_ros.sh'
    handoff = root / 'ros2_ws/src/agribot_vehicle_description/scripts/unity_sync_and_simulate.sh'
    for script in (wrapper, handoff):
        subprocess.run(['bash', '-n', str(script)], check=True)
        assert '--packages-ignore hikrobot_mvs_ros2' in script.read_text(), str(script)
