"""Software-only real Nav2 action/zero-output/patience test in localhost domain 225."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import signal
import subprocess
import time

import rclpy
from geometry_msgs.msg import TransformStamped, Twist, PoseStamped
from lifecycle_msgs.msg import Transition
from lifecycle_msgs.srv import ChangeState
from nav2_msgs.action import FollowPath
from nav_msgs.msg import OccupancyGrid, Odometry, Path as NavPath
from rclpy.action import ActionClient
from rclpy.node import Node
from rclpy.qos import QoSProfile, DurabilityPolicy
from sensor_msgs.msg import PointCloud2, PointField
from tf2_ros import StaticTransformBroadcaster
import yaml
from ament_index_python.packages import get_package_prefix


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if os.environ.get('ROS_DOMAIN_ID') != '225' or os.environ.get('ROS_LOCALHOST_ONLY') != '1':
        raise RuntimeError('Only the software-only localhost domain 225 is permitted')
    args.output.mkdir(parents=True, exist_ok=False)
    config = yaml.safe_load(args.config.read_text())
    def wall_clock(item):
        if isinstance(item, dict):
            for key, value in item.items():
                if key == 'use_sim_time':
                    item[key] = False
                else:
                    wall_clock(value)
    wall_clock(config)
    fixture = args.output/'fixture.yaml'
    fixture.write_text(yaml.safe_dump(config))
    patience = config['controller_server']['ros__parameters']['failure_tolerance']
    if not 0 < patience < 10:
        raise ValueError('An existing finite positive controller patience is required')
    report = dict(passed=False, hardware_outputs=False, controller_patience_seconds=patience,
        scope='Synthetic blocked/free costmap inputs to the real controller server; no CAN, chassis or frozen HIL route is exercised',
        baseline_sha256=hashlib.sha256(args.config.read_bytes()).hexdigest(), commands=[], checks={})
    rclpy.init()
    node = Node('hil_patience_readonly_test_client')
    log = (args.output/'controller.log').open('x')
    environment = dict(os.environ, ROS_LOG_DIR=str(args.output/'ros-log'))
    executable = str(Path(get_package_prefix('nav2_controller'))/'lib/nav2_controller/controller_server')
    server = subprocess.Popen([executable, '--ros-args',
                               '--params-file', str(fixture)], stdout=log, stderr=subprocess.STDOUT,
                              env=environment)
    map_pub = node.create_publisher(OccupancyGrid, '/map',
        QoSProfile(depth=1, durability=DurabilityPolicy.TRANSIENT_LOCAL))
    odom_topic = config['controller_server']['ros__parameters']['odom_topic']
    odom_pub = node.create_publisher(Odometry, odom_topic, 1)
    clouds = [node.create_publisher(PointCloud2, name, 1) for name in ('/configured/obstacle_points', '/lidar/points')]
    transforms = StaticTransformBroadcaster(node)
    static = []
    for parent, child in [('map', 'odom'), ('odom', 'base_link')]:
        tf = TransformStamped()
        tf.header.frame_id, tf.child_frame_id = parent, child
        tf.header.stamp = node.get_clock().now().to_msg()
        tf.transform.rotation.w = 1.
        static.append(tf)
    transforms.sendTransform(static)
    node.create_subscription(Twist, '/cmd_vel', lambda m: report['commands'].append(dict(
        at=time.monotonic(), linear_x=m.linear.x, angular_z=m.angular.z)), 20)
    blocked, last = True, 0.
    active_goal = None

    def pump(seconds, until=None):
        nonlocal last
        end = time.monotonic()+seconds
        while time.monotonic() < end:
            if server.poll() is not None:
                raise RuntimeError('Controller server exited during the fixture')
            now = time.monotonic()
            if now-last >= .05:
                stamp = node.get_clock().now().to_msg()
                grid = OccupancyGrid()
                grid.header.frame_id, grid.header.stamp = 'map', stamp
                grid.info.width = grid.info.height = 80
                grid.info.resolution = .1
                grid.info.origin.position.x = grid.info.origin.position.y = -4.
                grid.info.origin.orientation.w = 1.
                grid.data = [100 if blocked else 0]*6400
                map_pub.publish(grid)
                odom = Odometry()
                odom.header.frame_id, odom.child_frame_id, odom.header.stamp = 'odom', 'base_link', stamp
                odom.pose.pose.orientation.w = 1.
                odom_pub.publish(odom)
                cloud = PointCloud2()
                cloud.header.frame_id, cloud.header.stamp = 'base_link', stamp
                cloud.height, cloud.point_step, cloud.row_step = 1, 12, 0
                cloud.fields = [PointField(name=name, offset=4*i, datatype=PointField.FLOAT32, count=1)
                                for i, name in enumerate(('x', 'y', 'z'))]
                for publisher in clouds:
                    publisher.publish(cloud)
                last = now
            rclpy.spin_once(node, timeout_sec=.01)
            if until is not None and until():
                return True
        return False

    def wait(future, timeout=10.):
        if not pump(timeout, future.done):
            raise TimeoutError('Software Nav2 action/service timed out')
        return future.result()

    def follow(client):
        request = FollowPath.Goal()
        request.controller_id, request.goal_checker_id = 'FollowPath', 'goal_checker'
        request.path = NavPath()
        request.path.header.frame_id, request.path.header.stamp = 'map', node.get_clock().now().to_msg()
        for index in range(61):
            pose = PoseStamped()
            pose.header = request.path.header
            pose.pose.position.x, pose.pose.orientation.w = index*.05, 1.
            request.path.poses.append(pose)
        goal = wait(client.send_goal_async(request))
        if not goal.accepted:
            raise RuntimeError('Fixture path was rejected before controller execution')
        return goal

    try:
        service = node.create_client(ChangeState, '/controller_server/change_state')
        if not service.wait_for_service(timeout_sec=15.):
            raise RuntimeError('Controller lifecycle unavailable')
        pump(2.)
        for transition in (Transition.TRANSITION_CONFIGURE, Transition.TRANSITION_ACTIVATE):
            response = wait(service.call_async(ChangeState.Request(transition=Transition(id=transition))), 20.)
            if not response.success:
                raise RuntimeError('Controller lifecycle transition failed')
        pump(2.)
        client = ActionClient(node, FollowPath, '/follow_path')
        if not client.wait_for_server(timeout_sec=5.):
            raise RuntimeError('Native FollowPath unavailable')
        begin = time.monotonic()
        active_goal = follow(client)
        result = wait(active_goal.get_result_async(), patience+8.)
        elapsed = time.monotonic()-begin
        active_goal = None
        commands = [r for r in report['commands'] if r['at'] >= begin]
        report['blocked_status'], report['blocked_elapsed_wall_seconds'] = result.status, elapsed
        report['checks'].update(blocked_action_aborted=result.status == 6,
            blocked_output_is_zero=bool(commands) and all(abs(r['linear_x']) < 1e-9 and
                                                       abs(r['angular_z']) < 1e-9 for r in commands),
            existing_patience_is_bounded=patience <= elapsed < patience+5.)
        blocked = False
        pump(2.)
        begin = time.monotonic()
        active_goal = follow(client)
        moving = pump(5., lambda: any(r['at'] >= begin and r['linear_x'] > .001 for r in report['commands']))
        report['checks']['fresh_action_after_free_map_computes_motion'] = moving
        wait(active_goal.cancel_goal_async())
        result = wait(active_goal.get_result_async())
        active_goal = None
        report['checks']['cancel_is_confirmed'] = result.status == 5
        pump(.5)
        report['passed'] = all(report['checks'].values())
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        try:
            if active_goal is not None and server.poll() is None:
                wait(active_goal.cancel_goal_async(), 3.)
        finally:
            if server.poll() is None:
                server.send_signal(signal.SIGINT)
                try:
                    server.wait(timeout=15.)
                except subprocess.TimeoutExpired:
                    server.terminate()
                    server.wait(timeout=5.)
            report['server_exit_code'] = server.returncode
            if server.returncode not in (0, 130):
                report['passed'] = False
            log.close()
            (args.output/'result.json').write_text(json.dumps(report, indent=2)+'\n')
            node.destroy_node()
            rclpy.try_shutdown()
    print(json.dumps({k: v for k, v in report.items() if k != 'commands'}, indent=2))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
