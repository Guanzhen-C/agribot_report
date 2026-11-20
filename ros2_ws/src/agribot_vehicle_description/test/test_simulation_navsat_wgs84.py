import importlib.util
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET

import numpy as np
import pytest
from pyproj import Geod, Transformer
import yaml


def module():
    path = Path(__file__).resolve().parents[2]/"agribot_ackermann_mppi/scripts/sim_navsat_bridge.py"
    spec = importlib.util.spec_from_file_location("simulation_navsat_wgs84", path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def configured_launch():
    package = Path(__file__).resolve().parents[1]
    spec = importlib.util.spec_from_file_location("configured_sim_navsat", package/"launch/configured_ackermann_sim.launch.py")
    launch = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(launch)
    return launch


@pytest.mark.parametrize("reference", [(30.5, 114., 20.), (38.968032404, 115.90811917, -4.4333)])
@pytest.mark.parametrize("enu", [(0., 0., 0.), (0., 1000., 0.), (800., -600., 2.), (-1000., 500., -1.)])
def test_gnss_frame_round_trip_without_spherical_scale_error(reference, enu):
    frame = module().Wgs84Enu(*reference)
    lat, lon, alt = frame.geodetic(*enu)
    # PROJ's independent topocentric pipeline is the acceptance reference.
    pipeline = Transformer.from_pipeline(
        f"+proj=pipeline +step +proj=cart +ellps=WGS84 +step +proj=topocentric +ellps=WGS84 "
        f"+lat_0={reference[0]} +lon_0={reference[1]} +h_0={reference[2]}")
    restored = pipeline.transform(lon, lat, alt)
    np.testing.assert_allclose(restored, enu, atol=1e-6)


def test_one_kilometer_north_is_not_several_meters_short():
    frame = module().Wgs84Enu(30.5, 114., 20.)
    lat, lon, unused = frame.geodetic(0, 1000, 0)
    unused1, unused2, distance = Geod(ellps="WGS84").inv(114., 30.5, lon, lat)
    assert distance == pytest.approx(1000, abs=.01)


def test_dedicated_physics_truth_only_changes_the_observation_topic(tmp_path, monkeypatch):
    package = Path(__file__).resolve().parents[1]
    launch = configured_launch()
    monkeypatch.setattr(launch.tempfile, "gettempdir", lambda: str(tmp_path))
    source = package/"generated/unity_car3/models/unity_car3.sdf"
    original = source.read_bytes()
    standard = ET.parse(launch._write_gazebo_vehicle_model(source))
    observed = ET.parse(launch._write_gazebo_vehicle_model(source, ground_truth_topic="/validation/physics_truth"))
    query = "model/plugin[@name='configured_ground_truth']/ros/remapping"
    assert standard.findtext(query) == "odom:=base_pose_ground_truth"
    assert observed.findtext(query) == "odom:=/validation/physics_truth"
    observed.find(query).text = standard.findtext(query)
    assert ET.tostring(observed.getroot()) == ET.tostring(standard.getroot())
    assert source.read_bytes() == original


@pytest.mark.parametrize("scenario,vehicle_id", [
    ("orchard", "ackermann_current"), ("arable_field", "ackermann_current"),
    ("greenhouse", "ackermann_current"), ("farmyard", "ackermann_current"),
    ("tomato_greenhouse", "ackermann_current"), ("strawberry_field", "ackermann_current"),
    ("vineyard", "ackermann_current"), ("vegetable_field", "ackermann_current"),
    ("wide_headland", "unity_car3"), ("external_scene", "unity_car2"),
])
def test_scene_enu_origin_matches_the_configured_gnss_generator(tmp_path, monkeypatch, scenario, vehicle_id):
    from agribot_farm_sim import ground, scenarios
    from launch import LaunchContext
    from launch.actions import IncludeLaunchDescription

    sim = configured_launch()
    monkeypatch.setattr(sim.tempfile, "gettempdir", lambda: str(tmp_path))
    monkeypatch.setattr(sim, "_gazebo_transport_compat_path", lambda: "")
    monkeypatch.setattr(ground, "prepare_orchard_ground", lambda source, directory: source)
    monkeypatch.setattr(scenarios, "build", lambda selected, directory: tmp_path)
    monkeypatch.setattr(scenarios, "write_vehicle_route", lambda *args: tmp_path/"route.yaml")
    external = ""
    if scenario == "external_scene":
        for name in ("world.sdf", "map.yaml", "route.yaml"):
            (tmp_path/name).touch()
        manifest = tmp_path/"scene.json"
        manifest.write_text(json.dumps(dict(schema_version=1, world="world.sdf", map="map.yaml",
                                            waypoints="route.yaml", spawn=[2.5, -3.0, 1.0], spawn_ground_z=.15)))
        external = str(manifest)
    ctx = LaunchContext()
    ctx.launch_configurations.update(vehicle_id=vehicle_id, scenario="orchard" if external else scenario,
                                     scene_manifest=external, nav2_params_override="", physical_truth_topic="",
                                     track_drive_plugin="", manipulator="false", arm_demo="false", vision_mode="off")
    args, = [a for entity in sim._launch_vehicle(ctx) if isinstance(entity, IncludeLaunchDescription)
             for a in [dict(entity.launch_arguments)] if "fastlivo_georeference_file" in a]
    x, y, z, yaw = [float(args["initial_pose_"+axis]) for axis in ("x", "y", "z", "yaw")]
    ax, ay, az = [float(args["sim_base_to_antenna_"+axis]) for axis in ("x", "y", "z")]
    reference = yaml.safe_load(Path(args["fastlivo_georeference_file"]).read_text())
    assert reference["map"]["id"] == scenario+"_configured_sim"
    assert reference["map_from_enu"]["xyz"] == pytest.approx(
        [x+math.cos(yaw)*ax-math.sin(yaw)*ay, y+math.sin(yaw)*ax+math.cos(yaw)*ay, z+az])
    if scenario == "orchard":
        assert (x, y, yaw) == (2.0, 36.0, 0.0)
