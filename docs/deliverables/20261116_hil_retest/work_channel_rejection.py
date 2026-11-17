"""Bounded wrong-implement requests through the ordinary physical HIL chain."""
import argparse
import json
from pathlib import Path
import socket
import time

import rclpy
from geometry_msgs.msg import Twist
from nav_msgs.msg import Odometry
from rclpy.node import Node
from rclpy.qos import qos_profile_sensor_data
from sensor_msgs.msg import JointState
from std_msgs.msg import String

from agribot_hil_apple.isobus_can_mux import CAN_FRAME
from agribot_hil_apple.tracked_work_wire import decode_drive, decode_work


def settled_work_window(rows, joint_count, duration=.5):
    if not rows:
        return False
    cutoff = rows[-1]['sim_time']-duration
    starts = [i for i, row in enumerate(rows) if row['sim_time'] <= cutoff]
    if not starts:
        return False
    window = rows[starts[-1]:]
    return (all(0. <= b['sim_time']-a['sim_time'] <= .15 for a, b in zip(window, window[1:]))
            and all(len(row['value']['velocity']) == joint_count and
                    all(abs(value) < .5 for value in row['value']['velocity']) for row in window))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--kind', choices=('sprayer', 'shredder'), required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    rclpy.init()
    node = Node('hil_wrong_implement_request_check')
    drive = node.create_publisher(Twist, '/hil/navigation/cmd_vel', 1)
    work = node.create_publisher(String, '/hil/work/command', 1)
    records, checks = [], {}
    phase = 'initializing'
    report = dict(passed=False, checks=checks, records=records,
                  scope='High-level requests only; execution and actual feedback remain on physical CAN')
    def receive(kind, msg):
        value = (dict(names=list(msg.name), velocity=list(msg.velocity)) if kind == 'joints' else
                 dict(speed=msg.twist.twist.linear.x))
        stamp = msg.header.stamp.sec+msg.header.stamp.nanosec*1e-9
        records.append(dict(at=time.monotonic(), phase=phase, kind=kind, sim_time=stamp, value=value))
    node.create_subscription(JointState, '/hil/work/joint_states', lambda m: receive('joints', m), 20)
    node.create_subscription(Odometry, '/hil/ground_truth', lambda m: receive('truth', m), qos_profile_sensor_data)
    bus = socket.socket(socket.PF_CAN, socket.SOCK_RAW | socket.SOCK_NONBLOCK, socket.CAN_RAW)
    bus.bind(('can0',))
    valid = ((3, 4, 0) if args.kind == 'sprayer' else (0, 0, 3500))
    invalid = ((0, 0, 3500) if args.kind == 'sprayer' else (3, 4, 0))
    joint_count = 2 if args.kind == 'sprayer' else 1
    names = ('fan_gear', 'pump_gear', 'shredder_level')

    def pump(name, seconds, values, velocity):
        nonlocal phase
        phase = name
        start, last = time.monotonic(), 0.
        records.append(dict(at=start, phase=name, kind='requested',
                            value=dict(zip(names, values)), speed=velocity))
        while time.monotonic()-start < seconds:
            now = time.monotonic()
            if now-last >= .05:
                target = Twist()
                target.linear.x = velocity
                drive.publish(target)
                work.publish(String(data=json.dumps(dict(zip(names, values)))))
                last = now
            for _ in range(128):
                try:
                    ident, length, data = CAN_FRAME.unpack(bus.recv(CAN_FRAME.size))
                except BlockingIOError:
                    break
                if ident in (0x514, 0x580) and length == 8:
                    records.append(dict(at=now, phase=name, kind='can', id=ident, data=data.hex()))
            rclpy.spin_once(node, timeout_sec=.01)
        return start

    def samples(kind, name, start, delay=1.5):
        return [r for r in records if r['phase'] == name and r['kind'] == kind and r['at'] >= start+delay]

    try:
        pump('neutral', 2., (0, 0, 0), 0.)
        start = pump('wrong_implement', 5., invalid, .10)
        native = samples('can', 'wrong_implement', start)
        work_values = [decode_work(bytes.fromhex(r['data'])) for r in native if r['id'] == 0x580]
        drive_values = [decode_drive(bytes.fromhex(r['data'])) for r in native if r['id'] == 0x514]
        actual = samples('joints', 'wrong_implement', start)
        checks.update(
            wrong_channel_outputs_zero=bool(work_values) and all(v == (0, 0, 0) for v in work_values),
            actual_wrong_channel_stopped=bool(actual) and all(len(r['value']['velocity']) == joint_count and
                                                           all(abs(v) < .5 for v in r['value']['velocity'])
                                                           for r in actual),
            legal_drive_not_corrupted=bool(drive_values) and all(v[0] > .05 for v in drive_values))
        start = pump('new_legal_request', 5., valid, .10)
        native = samples('can', 'new_legal_request', start)
        actual = samples('joints', 'new_legal_request', start)
        values = [decode_work(bytes.fromhex(r['data'])) for r in native if r['id'] == 0x580]
        checks.update(new_legal_work_received=bool(values) and all(v == valid for v in values),
                      actual_work_recovers=bool(actual) and all(len(r['value']['velocity']) == joint_count and
                                                             all(v > 50 for v in r['value']['velocity'])
                                                             for r in actual))
        start = pump('stop', 4., (0, 0, 0), 0.)
        joints = samples('joints', 'stop', start, delay=0.)
        truth = samples('truth', 'stop', start)
        report['stop_measurement'] = dict(
            method='End-of-observation stable half-second simulation window, not braking-transient samples',
            first_below_existing_speed_limit_wall_seconds=next((r['at']-start for r in joints
                if all(abs(v) < .5 for v in r['value']['velocity'])), None),
            stable_window_sim_seconds=.5, unchanged_joint_speed_limit=.5)
        checks.update(actual_work_stopped=settled_work_window(joints, joint_count),
                      actual_chassis_stopped=bool(truth) and all(abs(r['value']['speed']) < .02 for r in truth))
        report['passed'] = bool(checks and all(checks.values()))
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        try:
            pump('final_neutral', .5, (0, 0, 0), 0.)
        except BaseException as error:
            report.update(passed=False, cleanup_error=str(error))
            raise
        finally:
            args.output.write_text(json.dumps(report, indent=2)+'\n')
            bus.close()
            node.destroy_node()
            rclpy.try_shutdown()
    print(json.dumps(dict(passed=report['passed'], checks=checks)))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
