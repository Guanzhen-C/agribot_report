"""Finite read-only recorder for Car3/Car4, with the original sampling schema."""
import argparse
import errno
import json
from pathlib import Path
import socket
import struct
import time

import rclpy
from rclpy.node import Node
from rclpy.qos import qos_profile_sensor_data
from nav_msgs.msg import Odometry
from rosgraph_msgs.msg import Clock
from sensor_msgs.msg import JointState
from std_msgs.msg import Bool, Float64MultiArray, String


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--run', required=True)
    parser.add_argument('--profile', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    result, native = args.output/'independent.json', args.output/'native-can.jsonl'
    if result.exists() or native.exists():
        raise FileExistsError('A new recorder output is required')
    state_file = args.root/'.local/hil/state.json'
    deadline = time.monotonic()+1800.
    while time.monotonic() < deadline:
        state = json.loads(state_file.read_text())
        if state.get('run_id') != args.run or state.get('phase') in ('failed', 'stopped'):
            raise RuntimeError('Run ended before observer startup')
        if state.get('phase') in ('starting_x5', 'waiting_ready', 'starting_mission', 'running'):
            break
        time.sleep(.2)
    else:
        raise TimeoutError('HIL runtime did not start')
    rclpy.init(args=[])
    node = Node('hil_other_profile_readonly_observer')
    report = dict(run_id=args.run, profile=args.profile, counts={}, samples={}, transport_events=[],
        scope='Independent subscriptions and passive CAN reception; no command publishers')
    latest, sampled, clock = {}, {}, [0.]
    start = time.monotonic()

    def receive(name, msg):
        if isinstance(msg, Clock):
            clock[0] = msg.clock.sec+msg.clock.nanosec*1e-9
            return
        report['counts'][name] = report['counts'].get(name, 0)+1
        now = time.monotonic()-start
        if now-sampled.get(name, -1.) < .2:
            return
        sampled[name] = now
        if isinstance(msg, Odometry):
            p, q, v, w = msg.pose.pose.position, msg.pose.pose.orientation, msg.twist.twist.linear, msg.twist.twist.angular
            value = dict(position=[p.x, p.y, p.z], quaternion=[q.x, q.y, q.z, q.w],
                velocity=[v.x, v.y, v.z], angular_velocity=[w.x, w.y, w.z])
        elif isinstance(msg, JointState):
            value = dict(positions=dict(zip(msg.name, msg.position)), velocities=dict(zip(msg.name, msg.velocity)))
        elif isinstance(msg, String):
            value = json.loads(msg.data)
        else:
            value = list(msg.data) if isinstance(msg, Float64MultiArray) else msg.data
        latest[name] = value
        report['samples'].setdefault(name, []).append([now, clock[0], value])

    topics = [('clock', '/clock', Clock)]
    if args.profile.startswith('tracked_'):
        topics += [('truth', '/hil/ground_truth', Odometry), ('fused', '/fastlivo_rtk/odometry', Odometry),
            ('wheel', '/hil/wheel/odometry', Odometry), ('work', '/hil/work/joint_states', JointState),
            ('ready', '/fastlivo_rtk/ready', Bool), ('status', '/hil/tracked_can/status', String)]
    else:
        topics += [('truth', '/hil_hitch/ground_truth', Odometry), ('fused', '/odometry/filtered_navsat', Odometry),
            ('hitch', '/hil_hitch/plant_state', Float64MultiArray),
            ('power', '/hil_hitch/powertrain/plant_state', Float64MultiArray),
            ('can_status', '/hil_hitch/can/status', String),
            ('status', '/hil_hitch/powertrain/status', String), ('joints', '/joint_states', JointState)]
    subscriptions = [node.create_subscription(kind, topic, lambda msg, key=name: receive(key, msg),
        qos_profile_sensor_data) for name, topic, kind in topics]
    bus = socket.socket(socket.PF_CAN, socket.SOCK_RAW | socket.SOCK_NONBLOCK, socket.CAN_RAW)
    bus.bind(('can0',))
    frame, last = struct.Struct('=IB3x8s'), 0.
    try:
        with native.open('x') as stream:
            while time.monotonic() < deadline:
                rclpy.spin_once(node, timeout_sec=.02)
                for _ in range(256):
                    try:
                        raw = bus.recv(frame.size)
                    except BlockingIOError:
                        break
                    except OSError as error:
                        if error.errno != errno.ENETDOWN:
                            raise
                        report['transport_events'].append(dict(wall=time.monotonic()-start,
                            sim_time=clock[0], error=str(error)))
                        break
                    identifier, size, data = frame.unpack(raw)
                    stream.write(json.dumps([time.monotonic()-start, clock[0], identifier, size, data[:size].hex()])+'\n')
                now = time.monotonic()
                if now-last < 5.:
                    continue
                state = json.loads(state_file.read_text())
                report.update(terminal_state=state, wall_seconds=now-start, final_sim_time=clock[0])
                temporary = result.with_suffix('.tmp')
                temporary.write_text(json.dumps(report, indent=2)+'\n')
                temporary.replace(result)
                print(json.dumps(dict(phase=state.get('phase'), profile=args.profile, sim_time=clock[0],
                    truth=latest.get('truth', {}).get('position'))), flush=True)
                stream.flush()
                last = now
                if state.get('run_id') != args.run or state.get('phase') in ('completed', 'failed', 'stopped'):
                    break
    finally:
        bus.close()
        node.destroy_node()
        rclpy.try_shutdown()


if __name__ == '__main__':
    main()
