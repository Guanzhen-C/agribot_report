from pathlib import Path


def test_default_gazebo_transport_does_not_require_a_docker_interface():
    root = Path(__file__).resolve().parents[2]
    launch = root / 'ros2_ws/src/agribot_ackermann_mppi/launch/ackermann_waypoint_depth_collect_sim.launch.py'
    text = launch.read_text()
    assert 'os.environ.get("GAZEBO_IP", "127.0.0.1")' in text
    assert 'os.environ.get("GAZEBO_IP", "172.17.0.1")' not in text
    assert 'os.path.join(ackermann_share, "models")' in text


def test_orchard_external_models_are_shipped_locally():
    root = Path(__file__).resolve().parents[2]
    models = root / 'ros2_ws/src/agribot_ackermann_mppi/models'
    for name in ('jersey_barrier', 'oak_tree', 'pine_tree', 'polaris_ranger_ev'):
        assert (models / name / 'model.config').is_file()
        assert (models / name / 'model.sdf').is_file()
        assert any((models / name / 'meshes').iterdir())
    assert (models / 'GAZEBO_MODELS_LICENSE').is_file()
