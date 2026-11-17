"""Request nonzero high-level outputs while verified HIL authorization is disabled."""
import argparse
import json
import math
from pathlib import Path
import time

import rclpy
from geometry_msgs.msg import Twist
from nav_msgs.msg import Odometry
from rcl_interfaces.srv import GetParameters
from rclpy.node import Node
from rclpy.qos import qos_profile_sensor_data
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray, String


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--car', choices=('Car0', 'Car3', 'Car4'), required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--kind', choices=('sprayer', 'shredder'), default='sprayer')
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    rclpy.init()
    node = Node('hil_no_authority_nonzero_request_check')
    report = dict(passed=False, car=args.car, requests=[], samples=[], verified_parameters={})
    checks = ({'/hil_hitch_rdk_client': ['drive_authorized', 'work_authorized']} if args.car == 'Car4' else
              {'/hil_tracked_gazebo_can_bridge': ['motion_authorized', 'work_authorized'],
               '/hil_isobus_work_client': ['work_authorized']} if args.car == 'Car3' else
              {'/hil_gazebo_can_bridge': ['motion_authorized']})
    prefix = '/hil_hitch' if args.car == 'Car4' else '/hil'
    latest = {}
    def receive(kind, value):
        latest[kind] = value
        report['samples'].append(dict(at=time.monotonic(), kind=kind, value=value))
    node.create_subscription(Odometry, prefix+'/ground_truth', lambda m: receive('truth',
        [m.twist.twist.linear.x, m.twist.twist.linear.y, m.twist.twist.angular.z]), qos_profile_sensor_data)
    if args.car == 'Car3':
        node.create_subscription(JointState, '/hil/work/joint_states',
            lambda m: receive('work', list(m.velocity)), qos_profile_sensor_data)
    if args.car == 'Car4':
        node.create_subscription(Float64MultiArray, '/hil_hitch/plant_state',
            lambda m: receive('hitch', list(m.data)), qos_profile_sensor_data)
        node.create_subscription(Float64MultiArray, '/hil_hitch/powertrain/plant_state',
            lambda m: receive('power', list(m.data)), qos_profile_sensor_data)
    drive = node.create_publisher(Twist, '/nav2/cmd_vel' if args.car == 'Car4' else '/hil/navigation/cmd_vel', 1)
    publishers = []
    if args.car == 'Car3':
        publishers.append((node.create_publisher(String, '/hil/work/command', 1),
                           dict(fan_gear=3, pump_gear=4, shredder_level=0) if args.kind == 'sprayer' else
                           dict(fan_gear=0, pump_gear=0, shredder_level=3500)))
    if args.car == 'Car4':
        publishers += [(node.create_publisher(String, '/hil_hitch/command', 1),
                        dict(mode='position', position=9000, speed=80)),
                       (node.create_publisher(String, '/hil_hitch/powertrain/command', 1),
                        dict(mode='powershift_auto', gear=0, engine_rpm=1600, ratio=0, shift_ms=600, ratio_rate=150))]
    began = None
    try:
        for name, parameters in checks.items():
            client = node.create_client(GetParameters, name+'/get_parameters')
            try:
                if not client.wait_for_service(timeout_sec=10.):
                    raise RuntimeError('Authorization service unavailable: '+name)
                future = client.call_async(GetParameters.Request(names=parameters))
                rclpy.spin_until_future_complete(node, future, timeout_sec=10.)
                if not future.done() or future.result() is None:
                    raise RuntimeError('Authorization readback timed out')
                values = future.result().values
                if len(values) != len(parameters) or any(v.type != 1 or v.bool_value for v in values):
                    raise RuntimeError('Bench must already be explicitly unauthorized')
                report['verified_parameters'][name] = dict(zip(parameters, [v.bool_value for v in values]))
            finally:
                node.destroy_client(client)
        began, last = time.monotonic(), 0.
        report['requested_drive'] = dict(linear_x=.12, angular_z=0.)
        while time.monotonic()-began < 6.:
            now = time.monotonic()
            if now-last >= .05:
                target = Twist()
                target.linear.x = .12
                drive.publish(target)
                for pub, values in publishers:
                    pub.publish(String(data=json.dumps(values)))
                report['requests'].append(now)
                last = now
            rclpy.spin_once(node, timeout_sec=.01)
        observed = [s for s in report['samples'] if s['at'] >= began+2.]
        truth = [s['value'] for s in observed if s['kind'] == 'truth']
        report['checks'] = dict(nonzero_requests_transmitted=len(report['requests']) >= 50,
            chassis_stays_stopped=bool(truth) and all(math.hypot(v[0], v[1]) <= .02 and abs(v[2]) <= .02
                                                     for v in truth))
        if args.car == 'Car3':
            work = [s['value'] for s in observed if s['kind'] == 'work']
            report['checks']['work_stays_stopped'] = bool(work) and all(v and all(abs(w) <= .5 for w in v) for v in work)
        if args.car == 'Car4':
            hitch = [s['value'] for s in observed if s['kind'] == 'hitch']
            power = [s['value'] for s in observed if s['kind'] == 'power']
            report['checks'].update(hitch_stays_held=bool(hitch) and max(v[0] for v in hitch)-min(v[0] for v in hitch) < 50,
                power_stays_disabled=bool(power) and all(v[0] == 0 and v[19] == 0 and v[15] == 1 for v in power))
        report['passed'] = all(report['checks'].values())
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        try:
            for _ in range(12):
                drive.publish(Twist())
                rclpy.spin_once(node, timeout_sec=.02)
        finally:
            args.output.write_text(json.dumps(report, indent=2)+'\n')
            node.destroy_node()
            rclpy.try_shutdown()
    print(json.dumps(dict(passed=report['passed'], checks=report.get('checks'))))
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
