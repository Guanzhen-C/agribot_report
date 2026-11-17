from pathlib import Path
import os
import subprocess

ROOT = Path(__file__).parents[2]
SCRIPT = ROOT/'ros2_ws/src/agribot_vehicle_description/scripts/unity_sync_and_simulate.sh'


def test_stop_prevents_a_terminal_launch_that_has_not_started_yet(tmp_path):
    workspace=tmp_path/'empty-workspace'
    workspace.mkdir()
    fence=tmp_path/'launch-fence'
    fence.write_text('after-stop')
    env=dict(os.environ, AGRIBOT_ROS_WORKSPACE=str(workspace), XDG_STATE_HOME=str(tmp_path/'state'))
    stopped=subprocess.run(['bash',str(SCRIPT),'--stop'],env=env,capture_output=True,text=True,timeout=10)
    assert stopped.returncode==0,stopped.stderr
    delayed=subprocess.run(['bash',str(SCRIPT),'--export-dir',str(tmp_path),
        '--launch-fence-file',str(fence),'--launch-fence-token','before-stop'],
        env=env,capture_output=True,text=True,timeout=10)
    assert delayed.returncode==0,delayed.stderr
    assert '启动请求已取消' in delayed.stdout
    assert (tmp_path/'state/agribot-unity-sim/status').read_text().strip()=='stopped: launch cancelled'
    assert not (tmp_path/'state/agribot-unity-sim/simulation.sid').exists()
