"""Capture the actual HIL GUI during work or raised travel; no actuator input."""
import argparse
import json
import math
from pathlib import Path
import subprocess
import time

import rclpy
from nav_msgs.msg import Odometry
from rclpy.node import Node
from rclpy.qos import qos_profile_sensor_data
from std_msgs.msg import Float64MultiArray


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--condition', choices=('work', 'raised'), required=True)
    parser.add_argument('--state', type=Path, required=True)
    parser.add_argument('--run', required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    rclpy.init()
    node = Node('hil_harrow_gui_readonly')
    state = dict(work_seen=False)
    node.create_subscription(Odometry, '/hil_hitch/ground_truth',
        lambda m: state.update(truth=m, truth_at=time.monotonic()), qos_profile_sensor_data)
    def plant(message):
        values = list(message.data)
        state.update(plant=values, plant_at=time.monotonic(), work_seen=state['work_seen'] or values[2] == 1)
    node.create_subscription(Float64MultiArray, '/hil_hitch/plant_state', plant, qos_profile_sensor_data)
    def condition():
        session = json.loads(args.state.read_text())
        if session.get('run_id') != args.run or session.get('phase') != 'running':
            raise RuntimeError('The selected physical run is no longer running')
        if not all(k in state for k in ('truth', 'plant')) or \
                max(time.monotonic()-state[k] for k in ('truth_at', 'plant_at')) > .5:
            return False
        p = state['plant']
        return p[2] == 1 if args.condition == 'work' else state['work_seen'] and p[2] == 0 and p[3] > .08 and p[4] > .05
    report = dict(visual_review='pending', scope='Actual Gazebo GUI, camera pose changes only; no model/actuator commands',
                  condition=args.condition, screenshots=[])
    try:
        deadline = time.monotonic()+300.
        while not condition() and time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=.1)
        if not condition():
            raise TimeoutError('Requested actual working/raised motion state was not observed')
        topics = subprocess.check_output(['gz', 'topic', '-l'], text=True, timeout=10.).splitlines()
        cameras = [t for t in topics if t.endswith('/user_camera/joy_pose')]
        windows = subprocess.check_output(['xdotool', 'search', '--onlyvisible', '--name', '^Gazebo$'], text=True).split()
        if len(cameras) != 1 or len(windows) != 1:
            raise RuntimeError('Exactly one isolated visible Gazebo GUI is required')
        for label, side in [('rear-left', 1.), ('rear-right', -1.)]:
            p, q = state['truth'].pose.pose.position, state['truth'].pose.pose.orientation
            yaw = math.atan2(2*(q.w*q.z+q.x*q.y), 1-2*(q.y*q.y+q.z*q.z))
            target = [p.x-1.5*math.cos(yaw), p.y-1.5*math.sin(yaw), .8]
            position = [p.x-7*math.cos(yaw)-side*4*math.sin(yaw),
                        p.y-7*math.sin(yaw)+side*4*math.cos(yaw), 4.8]
            delta = [b-a for a, b in zip(position, target)]
            y = math.atan2(delta[1], delta[0])
            pitch = -math.atan2(delta[2], math.hypot(delta[0], delta[1]))
            quat = [-math.sin(pitch/2)*math.sin(y/2), math.sin(pitch/2)*math.cos(y/2),
                    math.cos(pitch/2)*math.sin(y/2), math.cos(pitch/2)*math.cos(y/2)]
            message = 'position {x:%s y:%s z:%s} orientation {x:%s y:%s z:%s w:%s}' % (*position, *quat)
            subprocess.run(['gz', 'topic', '-p', cameras[0], '-m', message], check=True, timeout=5.)
            until = time.monotonic()+.5
            while time.monotonic() < until:
                rclpy.spin_once(node, timeout_sec=.02)
            subprocess.run(['xdotool', 'windowactivate', '--sync', windows[0]], check=True, timeout=5.)
            subprocess.run(['xwd', '-silent', '-id', windows[0], '-out', str(args.output/(label+'.xwd'))],
                           check=True, timeout=5.)
            report['screenshots'].append(dict(file=label+'.xwd', actual_plant=state['plant'],
                camera_position=position, target=target, truth_sim_time=state['truth'].header.stamp.sec+
                state['truth'].header.stamp.nanosec*1e-9))
    finally:
        (args.output/'result.json').write_text(json.dumps(report, indent=2)+'\n')
        node.destroy_node()
        rclpy.try_shutdown()


if __name__ == '__main__':
    main()
