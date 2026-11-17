"""Physical native-CAN input expiry and bounded isolated X5 transport interruption."""
import argparse
import json
import math
from pathlib import Path
import select
import socket
import subprocess
import sys
import time

import rclpy
from geometry_msgs.msg import Twist
from nav_msgs.msg import Odometry
from rclpy.clock import Clock, ClockType
from rclpy.node import Node
from rclpy.qos import qos_profile_sensor_data
from std_msgs.msg import String

from agribot_hil_apple.can_wire import CAN_FRAME, decode_command
from agribot_hil_apple.tracked_work_wire import decode_drive


INTERRUPT = r'''
import json,os,pathlib,signal,sys,time
name=sys.argv[1]
matches=[]
for p in pathlib.Path('/proc').glob('[0-9]*/cmdline'):
    try:
        argv=p.read_bytes().split(b'\0')
        group=(p.parent/'cgroup').read_text()
        if not group.rstrip().endswith('/agribot-app-hil-rdk.service'): continue
        if any(pathlib.PurePosixPath(a.decode()).name==name for a in argv if a):
            matches.append(int(p.parent.name))
    except (OSError,UnicodeError): pass
if len(matches)!=1: raise RuntimeError('Exactly one isolated transport owner is required')
pid=matches[0]
signal.signal(signal.SIGHUP,signal.SIG_IGN)
print(json.dumps(dict(pid=pid,operation='bounded SIGSTOP',duration_wall_seconds=1.5)),flush=True)
try:
    os.kill(pid,signal.SIGSTOP)
    time.sleep(1.5)
finally:
    os.kill(pid,signal.SIGCONT)
print(json.dumps(dict(pid=pid,resumed=True)),flush=True)
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--car', choices=('Car0', 'Car3'), required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    sys.path.insert(0, str(args.root/'tools/hil'))
    from config import load, ssh_arguments
    config = load(validate_flash=False)
    rclpy.init()
    node = Node('hil_chassis_physical_watchdog_check')
    drive = node.create_publisher(Twist, '/hil/navigation/cmd_vel', 1)
    speed, renewing = [0.], [True]
    report = dict(passed=False, car=args.car, checks=[], samples=[], native=[], injections=[], bridge_status=[],
                  time_basis='All PC reaction/actual-motion samples use one local monotonic clock')
    truth, actual, native = [None], [None], [None]
    def receive(kind, message):
        now = time.monotonic()
        if kind == 'truth':
            v = message.twist.twist
            value = [math.hypot(v.linear.x, v.linear.y), v.angular.z]
            truth[0] = (now, value)
        else:
            value = [message.linear.x, message.angular.z]
            actual[0] = (now, value)
        report['samples'].append(dict(at=now, kind=kind, value=value))
    node.create_subscription(Odometry, '/hil/ground_truth', lambda m: receive('truth', m), qos_profile_sensor_data)
    node.create_subscription(Twist, '/hil/actuator/cmd_vel', lambda m: receive('actuator', m), 10)
    node.create_subscription(String, '/hil/can/status' if args.car == 'Car0' else '/hil/tracked_can/status',
        lambda m: report['bridge_status'].append(dict(at=time.monotonic(), value=json.loads(m.data))), 10)
    def publish():
        if renewing[0]:
            msg = Twist()
            msg.linear.x = speed[0]
            drive.publish(msg)
    node.create_timer(.05, publish, clock=Clock(clock_type=ClockType.STEADY_TIME))
    bus = socket.socket(socket.PF_CAN, socket.SOCK_RAW | socket.SOCK_NONBLOCK, socket.CAN_RAW)
    bus.bind(('can0',))
    identifier = 0x181 if args.car == 'Car0' else 0x514
    child = None
    def pump(seconds, condition=None):
        end = time.monotonic()+seconds
        while time.monotonic() < end:
            rclpy.spin_once(node, timeout_sec=.005)
            while select.select([bus], [], [], 0.)[0]:
                raw = bus.recv(CAN_FRAME.size)
                ident, length, data = CAN_FRAME.unpack(raw)
                if ident != identifier or length != 8:
                    continue
                value = (decode_command(data)[0] if args.car == 'Car0' else decode_drive(data)[0])
                native[0] = (time.monotonic(), value)
                report['native'].append(dict(at=native[0][0], id=ident, data=data.hex(), speed=value))
            if condition is not None and condition():
                return True
        return False
    def fresh(row):
        return row is not None and time.monotonic()-row[0] < .3
    def moving():
        return fresh(truth[0]) and truth[0][1][0] > .05 and fresh(native[0]) and native[0][1] > .05
    def stopped():
        return fresh(truth[0]) and truth[0][1][0] < .02 and fresh(actual[0]) and actual[0][1] == [0., 0.]
    def check(name, passed, **details):
        report['checks'].append(dict(name=name, passed=bool(passed), **details))
        if not passed:
            raise AssertionError(name)
    try:
        pump(1.)
        speed[0] = .12
        check('physical_nonzero_drive_before_input_expiry', pump(15., moving))
        renewing[0] = False
        began = time.monotonic()
        check('expired_high_level_input_produces_native_zero', pump(3., lambda:
            fresh(native[0]) and native[0][0] > began and abs(native[0][1]) < .001),
            elapsed_wall_seconds=time.monotonic()-began)
        check('actual_chassis_stops_on_input_expiry', pump(10., stopped))
        speed[0], renewing[0] = 0., True
        pump(1.)
        speed[0] = .12
        check('new_valid_input_recovers_actual_motion', pump(15., moving))
        target = 'ackermann_chassis_can_node' if args.car == 'Car0' else 'isobus_can_mux'
        log = (args.output.with_suffix('.interruption.log')).open('x')
        try:
            child = subprocess.Popen(ssh_arguments(config, 'python3', '-c', INTERRUPT, target),
                                     stdout=log, stderr=subprocess.STDOUT)
            began = time.monotonic()
            # Freeze only the native transport owner. The PC bridge keeps
            # publishing explicit zero after its unchanged native lease expires.
            check('physical_transport_gap_causes_zero_execution', pump(8., lambda:
                native[0] is not None and time.monotonic()-native[0][0] > .5
                and fresh(actual[0]) and actual[0][1] == [0., 0.]),
                last_native_at=native[0][0] if native[0] else None,
                observed_zero_at=actual[0][0] if actual[0] else None)
            neutral_at = time.monotonic()
            speed[0] = 0.
            while child.poll() is None and time.monotonic()-began < 15.:
                pump(.02)
            if child.poll() is None:
                raise TimeoutError('Bounded transport interruption did not finish')
            check('isolated_transport_owner_resumed', child.returncode == 0)
        finally:
            # The remote script has its own SIGCONT finally even if SSH drops.
            if child is not None and child.poll() is None:
                child.wait(timeout=10.)
            log.close()
        check('zero_target_and_fresh_native_feedback_settle', pump(15., lambda:
            stopped() and fresh(native[0]) and abs(native[0][1]) < .001))
        pump(1.)
        observed = [row for row in report['samples'] if row['at'] >= neutral_at]
        check('no_old_positive_target_replayed', bool(observed) and all(
            abs(row['value'][0]) < .001 and abs(row['value'][1]) < .001
            for row in observed if row['kind'] == 'actuator'))
        speed[0] = .12
        check('explicit_new_legal_target_recovers_after_transport', pump(15., moving))
        speed[0] = 0.
        check('final_actual_stop', pump(10., stopped))
        report['passed'] = True
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        speed[0], renewing[0] = 0., True
        try:
            pump(1.)
        finally:
            args.output.write_text(json.dumps(report, indent=2)+'\n')
            bus.close()
            node.destroy_node()
            rclpy.try_shutdown()
    print(json.dumps({k: v for k, v in report.items() if k not in ('samples', 'native')}, indent=2))


if __name__ == '__main__':
    main()
