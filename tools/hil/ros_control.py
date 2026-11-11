"""Isolated-domain readiness and neutral/disarm operations, not actuator bypasses."""
import argparse
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import time

import rclpy
from geometry_msgs.msg import Twist
from lifecycle_msgs.srv import GetState
from nav_msgs.msg import Odometry
from rcl_interfaces.srv import SetParameters
from rclpy.node import Node
from rclpy.parameter import Parameter
from rclpy.qos import qos_profile_sensor_data
from rosgraph_msgs.msg import Clock
from std_msgs.msg import Bool, Float64MultiArray, String


class Control(Node):
    def __init__(self, car, role='pc'):
        super().__init__('agribot_app_hil_control')
        self.car, self.role, self.data, self.lifecycle = car, role, {}, {}
        topics = [('/clock', Clock), ('/hil/can/status', String), ('/hil/tracked_can/status', String),
            ('/hil/isobus/events', String), ('/hil_hitch/isobus/events', String),
            ('/hil_hitch/plant_state', Float64MultiArray), ('/fastlivo_rtk/ready', Bool),
            ('/fastlivo_rtk/odometry', Odometry), ('/odometry/filtered_navsat', Odometry)]
        self.subscriptions_ = [self.create_subscription(kind, topic,
            lambda message, name=topic: self.receive(name, message), qos_profile_sensor_data) for topic, kind in topics]

    def receive(self, topic, message):
        if isinstance(message, String):
            try: value = json.loads(message.data)
            except ValueError: return
            if topic == '/hil/isobus/events':
                if value.get('kind') != 'gateway': return
            if topic == '/hil_hitch/isobus/events':
                if value.get('kind') != 'status': return
        elif isinstance(message, Bool): value = message.data
        elif isinstance(message, Float64MultiArray): value = list(message.data)
        elif isinstance(message, Clock): value = message.clock.sec+message.clock.nanosec*1e-9
        else:
            p = message.pose.pose.position
            value = [p.x, p.y, p.z]
            if not all(math.isfinite(v) for v in value): return
        self.data[topic] = (time.monotonic(), value)

    def fresh(self, topic, age=1.):
        item = self.data.get(topic)
        return item[1] if item and time.monotonic()-item[0] < age else None

    def physical_ready(self):
        if self.fresh('/clock') is None: return False
        if self.car == 'Car4':
            status = self.fresh('/hil_hitch/isobus/events', .5)
            plant = self.fresh('/hil_hitch/plant_state', .5)
            if not status or not plant or self.fresh('/odometry/filtered_navsat') is None: return False
            raw = bytes.fromhex(status['data'])
            return len(raw) == 8 and raw[1] == raw[2] == 1 and raw[3] == 0
        if self.fresh('/fastlivo_rtk/ready') is not True or self.fresh('/fastlivo_rtk/odometry') is None:
            return False
        if self.car == 'Car0':
            status = self.fresh('/hil/can/status', 1.5)
            return bool(status and not status.get('fault') and status.get('truth_fresh') and status.get('command_fresh'))
        status = self.fresh('/hil/tracked_can/status', 1.5)
        gateway = self.fresh('/hil/isobus/events', .75)
        if not status or not gateway: return False
        raw = bytes.fromhex(gateway['data'])
        mask = {'sprayer': 3, 'shredder': 4}.get(status.get('implement_kind'))
        return bool(not status.get('fault') and status.get('truth_fresh') and status.get('motors_fresh') and
            mask is not None and len(raw) == 8 and raw[0] == raw[1] == 1 and raw[2] & mask == mask)

    def ready(self, stop_file=None):
        clients = {name: self.create_client(GetState, '/'+name+'/get_state')
                   for name in ('planner_server', 'controller_server', 'map_server')}
        pending, last_request, stable, last_unit_check = {}, -math.inf, None, -math.inf
        deadline = time.monotonic()+120
        while time.monotonic() < deadline:
            rclpy.spin_once(self, timeout_sec=.05)
            now = time.monotonic()
            if stop_file is not None and stop_file.exists():
                raise InterruptedError('HIL启动已取消，正在清理')
            if now-last_unit_check > 1.:
                state = subprocess.run(['systemctl', '--user', 'is-active', 'agribot-app-hil-pc.service'],
                                       capture_output=True, text=True, timeout=5).stdout.strip()
                if state not in ('active', 'activating'):
                    raise RuntimeError('Gazebo或PC硬件反馈进程已退出，请查看本次pc-runtime.log')
                last_unit_check = now
            if now-last_request > 2.:
                for name, client in clients.items():
                    if client.service_is_ready(): pending[name] = client.call_async(GetState.Request())
                last_request = now
            for name, future in list(pending.items()):
                if future.done():
                    result = future.result()
                    self.lifecycle[name] = result.current_state.id if result else 0
                    del pending[name]
            good = self.physical_ready() and all(self.lifecycle.get(name) == 3 for name in clients)
            if good:
                if stable is None: stable = (now, self.fresh('/clock'))
                if now-stable[0] > .7 and self.fresh('/clock') > stable[1]:
                    return {'ready': True, 'lifecycle': self.lifecycle}
            else: stable = None
        diagnostic = {'lifecycle': self.lifecycle, 'topics': {
            name: {'age_sec': round(time.monotonic()-received, 2), 'value': value}
            for name, (received, value) in self.data.items()}}
        raise TimeoutError('HIL等待就绪超时；请检查CAN、定位、仿真时钟和X5算法日志。\n'+
                           json.dumps(diagnostic, ensure_ascii=False))

    def authorization(self, enabled):
        if self.car == 'Car4':
            requests = {'/hil_hitch_rdk_client': ['drive_authorized', 'work_authorized']} if self.role == 'rdk' else {}
            neutral = [('/hil_hitch/command', {'mode': 'hold', 'position': 0, 'speed': 0}),
                ('/hil_hitch/powertrain/command', {'mode': 'off', 'gear': 0, 'engine_rpm': 1600,
                    'ratio': 0, 'shift_ms': 600, 'ratio_rate': 150})]
        elif self.car == 'Car3':
            requests = ({'/hil_tracked_gazebo_can_bridge': ['motion_authorized', 'work_authorized']}
                        if self.role == 'pc' else {'/hil_isobus_work_client': ['work_authorized']})
            neutral = [('/hil/work/command', {'fan_gear': 0, 'pump_gear': 0, 'shredder_level': 0})]
        else:
            requests = {'/hil_gazebo_can_bridge': ['motion_authorized']} if self.role == 'pc' else {}
            neutral = []
        clients, futures = [], []
        missing = []
        for name, parameters in requests.items():
            client = self.create_client(SetParameters, name+'/set_parameters')
            clients.append(client)
            if not client.wait_for_service(timeout_sec=2.):
                if enabled: raise RuntimeError('HIL授权服务未就绪：'+name)
                missing.append(name)
                continue
            request = SetParameters.Request(parameters=[Parameter(key, value=enabled).to_parameter_msg() for key in parameters])
            futures.append(client.call_async(request))
        pubs = [self.create_publisher(Twist, name, 1) for name in ('/hil/navigation/cmd_vel', '/hil/drive/command', '/nav2/cmd_vel')]
        work = [(self.create_publisher(String, name, 1), String(data=json.dumps(value))) for name, value in neutral]
        deadline = time.monotonic()+2.
        while time.monotonic() < deadline:
            for publisher in pubs: publisher.publish(Twist())
            for publisher, message in work: publisher.publish(message)
            rclpy.spin_once(self, timeout_sec=.05)
        for future in futures:
            if not future.done() or future.result() is None or not all(result.successful for result in future.result().results):
                raise RuntimeError('HIL授权更新未收到成功应答。')
        return {'authorized': enabled, 'unavailable_nodes': missing,
                'note': 'Unavailable transports are stopped next; ECU input leases must expire before reflashing'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('ready', 'disarm', 'authorize'))
    parser.add_argument('--car', choices=('Car0', 'Car3', 'Car4'), required=True)
    parser.add_argument('--role', choices=('pc', 'rdk'), default='pc')
    parser.add_argument('--stop-file', type=Path)
    args = parser.parse_args()
    expected = '74' if args.car == 'Car4' else '73'
    if os.environ.get('ROS_DOMAIN_ID') != expected:
        raise ValueError('Refusing to control a non-HIL ROS domain')
    rclpy.init()
    node = Control(args.car, args.role)
    try: print(json.dumps(node.ready(args.stop_file) if args.action == 'ready' else node.authorization(args.action == 'authorize')))
    finally:
        node.destroy_node()
        rclpy.try_shutdown()


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
