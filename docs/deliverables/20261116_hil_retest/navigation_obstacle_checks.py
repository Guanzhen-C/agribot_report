"""Surveyed short obstacle test using actual X5 Smac/MPPI and physical CAN."""
import argparse
import ast
from collections import deque
import json
import math
from pathlib import Path
import time
import xml.etree.ElementTree as ET

import numpy as np
from matplotlib.path import Path as PolygonPath
import yaml
import rclpy
from gazebo_msgs.srv import SpawnEntity, DeleteEntity
from geometry_msgs.msg import PoseStamped, Twist
from nav2_msgs.action import ComputePathToPose, FollowPath
from nav_msgs.msg import OccupancyGrid, Odometry
from rclpy.action import ActionClient
from rclpy.clock import Clock, ClockType
from rclpy.node import Node
from rclpy.parameter import Parameter
from rclpy.qos import qos_profile_sensor_data
from rclpy.time import Time
from scipy.spatial.transform import Rotation
from sensor_msgs.msg import PointCloud2
from sensor_msgs_py import point_cloud2
from std_msgs.msg import String
from tf2_ros import Buffer, TransformListener, TransformException

from pose_audit import distances_to_path


def polygon_clearance(a, b):
    a, b = np.asarray(a, dtype=float), np.asarray(b, dtype=float)
    a, b = np.vstack([a, a[0]]), np.vstack([b, b[0]])
    if PolygonPath(a).intersects_path(PolygonPath(b), filled=True):
        return 0.
    return min(min(distances_to_path(a, b)), min(distances_to_path(b, a)))


def obstacle_xml(name):
    root = ET.Element('sdf', version='1.6')
    model = ET.SubElement(root, 'model', name=name)
    ET.SubElement(model, 'static').text = 'true'
    link = ET.SubElement(model, 'link', name='obstacle')
    for kind in ('collision', 'visual'):
        item = ET.SubElement(link, kind, name=kind)
        box = ET.SubElement(ET.SubElement(item, 'geometry'), 'box')
        ET.SubElement(box, 'size').text = '.5 .5 1.2'
    return ET.tostring(root, encoding='unicode')


def marked_box_cells(message, center, voxel_size=.1):
    resolution = message.info.resolution
    # A laser observes a box's faces, not its solid interior. Include cells
    # intersecting that physical box and its STVL voxel quantization, not a
    # large neighbourhood which might contain unrelated existing obstacles.
    half = .25+.5*(resolution+voxel_size)
    origin = message.info.origin.position
    left = max(0, math.floor((center[0]-half-origin.x)/resolution))
    right = min(message.info.width-1, math.floor((center[0]+half-origin.x)/resolution))
    bottom = max(0, math.floor((center[1]-half-origin.y)/resolution))
    top = min(message.info.height-1, math.floor((center[1]+half-origin.y)/resolution))
    return [[origin.x+(x+.5)*resolution, origin.y+(y+.5)*resolution]
            for y in range(bottom, top+1) for x in range(left, right+1)
            if message.data[y*message.info.width+x] == 100]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scenario', type=Path, required=True)
    parser.add_argument('--car', choices=('Car0', 'Car3', 'Car4'), required=True)
    parser.add_argument('--transmission', choices=('off', 'powershift_auto', 'cvt_auto'), default='off')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.car == 'Car4' and args.transmission == 'off':
        parser.error('Car4 requires its selected powertrain mode')
    args.output.mkdir(parents=True, exist_ok=False)
    rclpy.init()
    node = Node('hil_native_obstacle_acceptance', parameter_overrides=[Parameter('use_sim_time', value=True)])
    car4 = args.car == 'Car4'
    obstacle = [-13., .15] if car4 else [8., 36.15]
    target = [-7., 0.] if car4 else [14., 36.]
    tolerance = {'Car0': .2632937431335449, 'Car3': .4185015549046406, 'Car4': 1.0925620956374087}[args.car]
    config = yaml.safe_load((args.scenario/'nav2.yaml').read_text())
    footprint = ast.literal_eval(config['global_costmap']['global_costmap']['ros__parameters']['footprint'])
    obstacle_polygon = [[obstacle[0]+x, obstacle[1]+y] for x, y in [(-.25, -.25), (.25, -.25), (.25, .25), (-.25, .25)]]
    report = dict(passed=False, checks=[], truth=[], maps=[], clouds=[], plan=None,
        scope='Newly inserted physical Gazebo obstacle, actual X5 native planning/control and unchanged vehicle footprint',
        test_target=target, obstacle_center=obstacle, footprint=footprint,
        note='Dedicated surveyed acceptance segment; default mission manifests are not modified')
    latest, held = {}, [True]
    active = [None]
    def truth(message):
        p, q = message.pose.pose.position, message.pose.pose.orientation
        yaw = Rotation.from_quat([q.x, q.y, q.z, q.w]).as_euler('xyz')[2]
        rotation = np.array([[math.cos(yaw), -math.sin(yaw)], [math.sin(yaw), math.cos(yaw)]])
        body = np.asarray(footprint)@rotation.T+[p.x, p.y]
        value = dict(sim_time=message.header.stamp.sec+message.header.stamp.nanosec*1e-9,
                     xy=[p.x, p.y], speed=math.hypot(message.twist.twist.linear.x, message.twist.twist.linear.y),
                     clearance_m=polygon_clearance(body, obstacle_polygon))
        report['truth'].append(value)
        latest['truth'] = value
    node.create_subscription(Odometry, '/hil_hitch/ground_truth' if car4 else '/hil/ground_truth', truth, qos_profile_sensor_data)
    def costmap(kind, message):
        ix = math.floor((obstacle[0]-message.info.origin.position.x)/message.info.resolution)
        iy = math.floor((obstacle[1]-message.info.origin.position.y)/message.info.resolution)
        value = message.data[iy*message.info.width+ix] if 0 <= ix < message.info.width and 0 <= iy < message.info.height else None
        row = dict(kind=kind, sim_time=message.header.stamp.sec+message.header.stamp.nanosec*1e-9,
                   frame=message.header.frame_id, value=value,
                   observed_box_lethal_cells=marked_box_cells(message, obstacle))
        latest[kind] = row
        report['maps'].append(row)
    for kind in ('global', 'local'):
        node.create_subscription(OccupancyGrid, '/'+kind+'_costmap/costmap',
                                 lambda m, k=kind: costmap(k, m), qos_profile_sensor_data)
    buffer = Buffer()
    listener = TransformListener(buffer, node)
    pending_clouds, cloud_samples = deque(maxlen=12), {}
    def queue_cloud(topic, message):
        if time.monotonic()-cloud_samples.get(topic, -math.inf) >= 1.:
            cloud_samples[topic] = time.monotonic()
            pending_clouds.append((time.monotonic(), topic, message))
    for topic in ('/lidar/points', '/hil/lidar/self_filtered', '/configured/obstacle_points'):
        node.create_subscription(PointCloud2, topic, lambda m, t=topic: queue_cloud(t, m), qos_profile_sensor_data)
    def inspect_clouds():
        while pending_clouds and time.monotonic()-pending_clouds[0][0] > .5:
            _, topic, message = pending_clouds.popleft()
            row = dict(topic=topic, frame=message.header.frame_id,
                       stamp=message.header.stamp.sec+message.header.stamp.nanosec*1e-9)
            try:
                transform = buffer.lookup_transform('map', message.header.frame_id, Time.from_msg(message.header.stamp))
                q, t = transform.transform.rotation, transform.transform.translation
                points = np.asarray(list(point_cloud2.read_points(message,
                    field_names=('x', 'y', 'z'), skip_nans=True)), dtype=float).reshape(-1, 3)
                points = points@Rotation.from_quat([q.x, q.y, q.z, q.w]).as_matrix().T+[t.x, t.y, t.z]
                selected = points[(np.abs(points[:, 0]-obstacle[0]) <= .35) &
                                  (np.abs(points[:, 1]-obstacle[1]) <= .35) &
                                  (points[:, 2] > .03) & (points[:, 2] <= 1.3)]
                row.update(total_points=len(points), near_box_points=len(selected), near_box_xyz=selected[:20].tolist())
            except TransformException as error:
                row['transform_error'] = str(error)
            report['clouds'].append(row)
    node.create_timer(.1, inspect_clouds, clock=Clock(clock_type=ClockType.STEADY_TIME))
    stop = node.create_publisher(Twist, '/nav2/cmd_vel' if car4 else '/hil/navigation/cmd_vel', 1)
    work = node.create_publisher(String, '/hil/work/command', 1)
    hitch = node.create_publisher(String, '/hil_hitch/command', 1)
    power = node.create_publisher(String, '/hil_hitch/powertrain/command', 1)
    def renew():
        if car4:
            hitch.publish(String(data=json.dumps(dict(mode='position', position=9000, speed=80))))
            power.publish(String(data=json.dumps(dict(mode=args.transmission if held[0] else 'off', gear=0,
                engine_rpm=1600, ratio=0, shift_ms=600, ratio_rate=150))))
        else:
            work.publish(String(data='{"fan_gear":0,"pump_gear":0,"shredder_level":0}'))
    node.create_timer(.1, renew, clock=Clock(clock_type=ClockType.STEADY_TIME))
    planner = ActionClient(node, ComputePathToPose, '/compute_path_to_pose')
    controller = ActionClient(node, FollowPath, '/follow_path')
    def wait(future, timeout):
        rclpy.spin_until_future_complete(node, future, timeout_sec=timeout)
        if not future.done() or future.result() is None:
            raise TimeoutError('Native action/service response timed out')
        return future.result()
    def pump(seconds, condition=None):
        end = time.monotonic()+seconds
        while time.monotonic() < end:
            rclpy.spin_once(node, timeout_sec=.02)
            if condition is not None and condition():
                return True
        return False
    def service(name, kind, request):
        client = node.create_client(kind, name)
        try:
            if not client.wait_for_service(timeout_sec=5.):
                raise RuntimeError(name+' unavailable')
            result = wait(client.call_async(request), 15.)
            if not result.success:
                raise RuntimeError(name+': '+result.status_message)
        finally:
            node.destroy_client(client)
    def check(name, passed, **details):
        report['checks'].append(dict(name=name, passed=bool(passed), **details))
        if not passed:
            raise AssertionError(name)
    name, spawned = 'hil_acceptance_unmapped_obstacle', False
    try:
        check('actual_initial_pose_and_map_are_available', pump(10., lambda:
            'truth' in latest and latest.get('global', {}).get('value') is not None))
        baseline = latest['global']['value']
        check('surveyed_obstacle_position_initially_free', 0 <= baseline < 99 and
              not latest['global']['observed_box_lethal_cells'], cost=baseline)
        request = SpawnEntity.Request(name=name, xml=obstacle_xml(name), reference_frame='world')
        request.initial_pose.position.x, request.initial_pose.position.y = obstacle
        request.initial_pose.position.z, request.initial_pose.orientation.w = .6, 1.
        service('/spawn_entity', SpawnEntity, request)
        spawned = True
        # A rolling local map cannot show a point before the vehicle approaches
        # its window. Check global marking before planning, local marking in travel.
        check('new_obstacle_marks_global_costmap', pump(20., lambda:
            bool(latest.get('global', {}).get('observed_box_lethal_cells'))))
        if not planner.wait_for_server(timeout_sec=10.) or not controller.wait_for_server(timeout_sec=10.):
            raise RuntimeError('Actual X5 navigation action servers unavailable')
        goal_pose = PoseStamped()
        goal_pose.header.frame_id = 'map'
        goal_pose.header.stamp = node.get_clock().now().to_msg()
        goal_pose.pose.position.x, goal_pose.pose.position.y = target
        goal_pose.pose.orientation.w = 1.
        plan_goal = wait(planner.send_goal_async(ComputePathToPose.Goal(goal=goal_pose,
            planner_id='GridBased', use_start=False)), 10.)
        check('actual_smac_goal_accepted', plan_goal.accepted)
        active[0] = plan_goal
        plan_result = wait(plan_goal.get_result_async(), 120.)
        active[0] = None
        check('native_smac_returns_nonempty_path', plan_result.status == 4 and bool(plan_result.result.path.poses))
        path = plan_result.result.path
        report['plan'] = [[p.pose.position.x, p.pose.position.y] for p in path.poses]
        following = wait(controller.send_goal_async(FollowPath.Goal(path=path,
            controller_id='FollowPath', goal_checker_id='goal_checker')), 10.)
        check('actual_mppi_path_accepted', following.accepted)
        active[0] = following
        result = wait(following.get_result_async(), 240.)
        active[0] = None
        check('native_mppi_finishes_avoiding_obstacle', result.status == 4, action_status=result.status)
        finish = latest['truth']
        error = math.dist(finish['xy'], target)
        check('actual_final_position_within_half_wheelbase', error <= tolerance,
              error_m=error, tolerance_m=tolerance)
        minimum = min(row['clearance_m'] for row in report['truth'])
        check('actual_full_vehicle_footprint_does_not_contact_obstacle', minimum > 0.,
              minimum_clearance_m=minimum)
        check('actual_vehicle_passes_beyond_obstacle', finish['xy'][0] > obstacle[0]+1.)
        check('approaching_obstacle_marks_local_costmap',
              any(row['kind'] == 'local' and row['observed_box_lethal_cells'] for row in report['maps']))
        # Keep the real test obstacle present. A target inside it must fail
        # planning rather than dispatch another motion action or clear the map.
        goal_pose.header.stamp = node.get_clock().now().to_msg()
        goal_pose.pose.position.x, goal_pose.pose.position.y = obstacle
        blocked = wait(planner.send_goal_async(ComputePathToPose.Goal(goal=goal_pose,
            planner_id='GridBased', use_start=False)), 10.)
        check('occupied_goal_request_is_received', blocked.accepted)
        active[0] = blocked
        failure = wait(blocked.get_result_async(), 120.)
        active[0] = None
        check('occupied_goal_returns_explicit_planning_failure',
              failure.status == 6 and not failure.result.path.poses, action_status=failure.status)
        speed_limit = .03 if car4 else .02
        check('no_safe_path_leaves_actual_chassis_stopped', pump(10., lambda:
            latest['truth']['speed'] <= speed_limit), speed_limit_mps=speed_limit)
        report['stopped_after_planning_failure'] = latest['truth']
        report['passed'] = True
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        try:
            if active[0] is not None:
                response = wait(active[0].cancel_goal_async(), 8.)
                result = wait(active[0].get_result_async(), 8.)
                report['cancel_acknowledged'] = bool(response.goals_canceling)
                report['cancel_terminal_status'] = result.status
                if result.status not in (4, 5, 6):
                    raise RuntimeError('Native action termination was not confirmed')
            held[0] = False
            for _ in range(20):
                stop.publish(Twist())
                rclpy.spin_once(node, timeout_sec=.03)
            if spawned:
                service('/delete_entity', DeleteEntity, DeleteEntity.Request(name=name))
        except BaseException as error:
            report.update(passed=False, cleanup_error=str(error))
            raise
        finally:
            (args.output/'result.json').write_text(json.dumps(report, indent=2)+'\n')
            node.destroy_node()
            rclpy.try_shutdown()
    print(json.dumps({k: v for k, v in report.items() if k not in ('truth', 'maps', 'clouds', 'plan')}, indent=2))


if __name__ == '__main__':
    main()
