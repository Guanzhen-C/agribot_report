from pathlib import Path


def test_default_gazebo_transport_does_not_require_a_docker_interface():
    root = Path(__file__).resolve().parents[2]
    launch = root / 'ros2_ws/src/agribot_ackermann_mppi/launch/ackermann_waypoint_depth_collect_sim.launch.py'
    text = launch.read_text()
    assert 'os.environ.get("GAZEBO_IP", "127.0.0.1")' in text
    assert 'os.environ.get("GAZEBO_IP", "172.17.0.1")' not in text
