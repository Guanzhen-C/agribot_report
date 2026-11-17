"""Passive raw-cost and cloud diagnostics; never publish control or change maps."""
import argparse
from collections import deque
import json
import math
from pathlib import Path
import time

import numpy as np
import rclpy
from geometry_msgs.msg import Twist
from nav2_msgs.msg import Costmap
from nav_msgs.msg import Odometry
from rclpy.duration import Duration
from rclpy.node import Node
from rclpy.qos import qos_profile_sensor_data
from rclpy.time import Time
from rosgraph_msgs.msg import Clock
from sensor_msgs.msg import PointCloud2
from sensor_msgs_py import point_cloud2
from tf2_ros import Buffer, TransformListener, TransformException

from agribot_hil_apple.async_trace import AsyncTrace


def heading(q):
    return math.atan2(2*(q.w*q.z+q.x*q.y), 1-2*(q.y*q.y+q.z*q.z))


def body_coordinates(xy, pose):
    a = pose[3]
    return (xy-np.asarray(pose[:2])) @ np.array([[math.cos(a), -math.sin(a)],
                                                               [math.sin(a), math.cos(a)]])


def inside_body(body):
    return ((body[:, 0] >= -.5254415464) & (body[:, 0] <= .6515151453) &
            (body[:, 1] >= -.3926591766) & (body[:, 1] <= .3988280189))


def cells(data, metadata, pose):
    grid = np.asarray(data, dtype=np.uint8).reshape(metadata.size_y, metadata.size_x)
    rows, cols = np.nonzero((grid == 253) | (grid == 254))
    xy = np.column_stack((cols+.5, rows+.5))*metadata.resolution
    a = heading(metadata.origin.orientation)
    xy = xy @ np.array([[math.cos(a), math.sin(a)], [-math.sin(a), math.cos(a)]])
    xy += [metadata.origin.position.x, metadata.origin.position.y]
    body = body_coordinates(xy, pose)
    selected = np.linalg.norm(body, axis=1) < 2.
    values, xy, body = grid[rows, cols][selected], xy[selected], body[selected]
    inside = inside_body(body)
    return dict(lethal_254_inside=int(np.count_nonzero(inside & (values == 254))),
                inscribed_253_inside=int(np.count_nonzero(inside & (values == 253))),
                cells_map_cost=np.column_stack((xy, values)).round(5).tolist())


class Probe(Node):
    def __init__(self, stream):
        super().__init__('hil_raw_collision_probe')
        self.stream, self.pose, self.clock = stream, None, 0.
        self.pose_stamp, self.sampled = 0., {}
        self.kinematics = {}
        self.clouds = deque()
        self.buffer = Buffer()
        self.listener = TransformListener(self.buffer, self)
        self.create_subscription(Clock, '/clock', self.on_clock, qos_profile_sensor_data)
        self.create_subscription(Odometry, '/fastlivo_rtk/odometry', self.on_pose, qos_profile_sensor_data)
        for topic in ('/hil/wheel/odometry', '/hil/ground_truth'):
            self.create_subscription(Odometry, topic, lambda m, t=topic: self.on_motion(t, m),
                                     qos_profile_sensor_data)
        for topic in ('/hil/navigation/cmd_vel', '/hil/drive/command'):
            self.create_subscription(Twist, topic, lambda m, t=topic: self.kinematics.update({t:
                dict(at=time.monotonic(), linear_x=m.linear.x, angular_z=m.angular.z)}), 1)
        for topic in ('/local_costmap/costmap_raw', '/global_costmap/costmap_raw'):
            self.create_subscription(Costmap, topic, lambda m, t=topic: self.on_grid(t, m),
                                     qos_profile_sensor_data)
        for topic in ('/configured/obstacle_points', '/hil/lidar/self_filtered'):
            self.create_subscription(PointCloud2, topic, lambda m, t=topic: self.on_cloud(t, m),
                                     qos_profile_sensor_data)
        self.create_timer(.1, self.drain_clouds)

    def on_clock(self, msg):
        self.clock = msg.clock.sec+msg.clock.nanosec*1e-9

    def on_pose(self, msg):
        p = msg.pose.pose
        self.pose = [p.position.x, p.position.y, p.position.z, heading(p.orientation)]
        self.pose_stamp = msg.header.stamp.sec+msg.header.stamp.nanosec*1e-9

    def on_motion(self, topic, msg):
        p, v = msg.pose.pose, msg.twist.twist
        self.kinematics[topic] = dict(at=time.monotonic(),
            stamp=msg.header.stamp.sec+msg.header.stamp.nanosec*1e-9,
            pose=[p.position.x, p.position.y, p.position.z, heading(p.orientation)],
            linear=[v.linear.x, v.linear.y, v.linear.z], angular_z=v.angular.z)

    def sample(self, topic):
        now = time.monotonic()
        if self.pose is None or now-self.sampled.get(topic, -math.inf) < .25:
            return False
        self.sampled[topic] = now
        return True

    def entry(self, topic, msg):
        return dict(topic=topic, at=time.monotonic(), sim=self.clock, pose=self.pose,
                    pose_stamp=self.pose_stamp, frame=msg.header.frame_id,
                    kinematics=self.kinematics,
                    stamp=msg.header.stamp.sec+msg.header.stamp.nanosec*1e-9)

    def save(self, entry):
        self.stream.write(json.dumps(entry)+'\n')

    def on_grid(self, topic, msg):
        if not self.sample(topic):
            return
        entry = self.entry(topic, msg)
        if msg.header.frame_id != 'map':
            entry['error'] = 'Unexpected grid frame'
        else:
            entry.update(cells(msg.data, msg.metadata, self.pose))
        self.save(entry)

    def on_cloud(self, topic, msg):
        if not self.sample(topic):
            return
        if len(self.clouds) >= 32:
            self.save(dict(topic=topic, error='Diagnostic cloud queue full', sim=self.clock))
            return
        self.clouds.append((time.monotonic(), topic, msg))

    def drain_clouds(self):
        # X5 fused TF arrives after the PC's raw scan. Wait before requesting
        # that exact stamp, without ever substituting a latest-time transform.
        while self.clouds and time.monotonic()-self.clouds[0][0] >= .5:
            _, topic, msg = self.clouds.popleft()
            self.record_cloud(topic, msg)

    def record_cloud(self, topic, msg):
        entry = self.entry(topic, msg)
        try:
            raw = point_cloud2.read_points(msg, field_names=['x', 'y', 'z'], skip_nans=True)
            points = np.column_stack([raw[key].reshape(-1) for key in ('x', 'y', 'z')])
            tf = self.buffer.lookup_transform('map', msg.header.frame_id, Time.from_msg(msg.header.stamp),
                                              timeout=Duration(seconds=.005)).transform
            x, y, z, w = tf.rotation.x, tf.rotation.y, tf.rotation.z, tf.rotation.w
            rotation = np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                                 [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                                 [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])
            points = points @ rotation.T+[tf.translation.x, tf.translation.y, tf.translation.z]
            body = body_coordinates(points[:, :2], self.pose)
            inside = inside_body(body)
            nearest = np.linalg.norm(body, axis=1) < 1.25
            entry.update(total=len(points), points_inside=int(inside.sum()),
                         points_inside_map=points[inside].round(5).tolist(),
                         all_points_near_map=points[nearest].round(5).tolist())
        except (TransformException, ValueError) as error:
            entry['error'] = str(error)
        self.save(entry)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--seconds', type=float, default=1500.)
    parser.add_argument('--run-state', type=Path)
    parser.add_argument('--run')
    args = parser.parse_args()
    rclpy.init()
    stream = AsyncTrace(args.output)
    node = Probe(stream)
    try:
        deadline = time.monotonic()+args.seconds
        while rclpy.ok() and time.monotonic() < deadline:
            if args.run_state:
                state = json.loads(args.run_state.read_text())
                if state.get('run_id') != args.run or state.get('phase') in ('completed', 'failed', 'stopped'):
                    break
            rclpy.spin_once(node, timeout_sec=.1)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        stream.close()
        rclpy.try_shutdown()


if __name__ == '__main__':
    main()
