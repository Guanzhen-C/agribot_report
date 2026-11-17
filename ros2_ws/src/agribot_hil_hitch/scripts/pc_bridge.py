"""The sole ROS actuator publisher for the dedicated hitch native CAN plant."""
import argparse
import json
import math
from pathlib import Path
import socket
import struct
import time

import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, ReliabilityPolicy
from geometry_msgs.msg import Twist
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray, Int16, String
from agribot_hil_apple.physical_can_guard import PhysicalTxGuard, temporary_tx_error
from agribot_hil_apple.async_trace import AsyncTrace

from wire import DRIVE, DRIVE_STATE, VALVE, SENSOR, Lease, decode_drive, decode_valve, sensor_feedback, seal
from powertrain_wire import ENGINE, TRANSMISSION, SENSORS, ActuatorPair, sensors

FRAME = struct.Struct('=IB3x8s')


def open_bus(interface, serial, sil):
    if sil:
        if not interface.startswith('vcan') or interface in ('vcan73', 'vcan74'):
            raise ValueError('SIL plant needs a separate virtual native bus')
    else:
        device = (Path('/sys/class/net')/interface/'device').resolve()
        usb = next((p for p in (device, *device.parents) if (p/'idVendor').is_file()), None)
        if usb is None or (usb/'serial').read_text().strip() != serial or \
                (device/'driver').resolve().name != 'gs_usb':
            raise ValueError('Unexpected physical USB-CAN; no fallback to virtual CAN')
        import subprocess
        data = json.loads(subprocess.check_output(['ip', '-j', '-d', 'link', 'show', interface]))[0]
        if data['linkinfo']['info_data']['bittiming']['bitrate'] != 250000 or 'UP' not in data['flags']:
            raise ValueError('Hitch bench CAN must already be UP at 250 kbit/s')
    bus = socket.socket(socket.PF_CAN, socket.SOCK_RAW | socket.SOCK_NONBLOCK, socket.CAN_RAW)
    bus.bind((interface,))
    return bus


class Bridge(Node):
    def __init__(self, args):
        super().__init__('hil_hitch_native_plant')
        self.declare_parameter('physical_can', not args.sil)
        self.declare_parameter('powertrain_force_plant', args.powertrain)
        self.bus = open_bus(args.interface, args.serial, args.sil)
        self.open_bus = lambda: open_bus(args.interface, args.serial, args.sil)
        self.tx_guard = None if args.sil else PhysicalTxGuard(args.interface, args.serial, 250000)
        self.fault, self.fault_since, self.errors = '', 0., 0
        self.recoverable_fault, self.awaiting_neutral = False, False
        self.transmitting, self.last_feedback_sent, self.last_status = False, 0., 0.
        self.trace = AsyncTrace(args.trace)
        self.state = None
        self.state_at = -math.inf
        self.steering = 0.
        self.drive_lease, self.valve_lease = Lease(), Lease()
        self.drive = (0., 0., False)
        self.valve = 0
        self.counter = 0
        self.power_pair = ActuatorPair()
        self.power_state = None; self.power_at = -math.inf
        self.drive_pub = self.create_publisher(Twist, '/hil_hitch/actuator/cmd_vel', 1)
        self.valve_pub = self.create_publisher(Int16, '/hil_hitch/actuator/valve', 1)
        self.power_pub = self.create_publisher(Float64MultiArray, '/hil_hitch/actuator/powertrain', 1)
        self.status_pub = self.create_publisher(String, '/hil_hitch/can/status', 1)
        feedback_qos = QoSProfile(depth=1, reliability=ReliabilityPolicy.BEST_EFFORT)
        self.create_subscription(Float64MultiArray, '/hil_hitch/powertrain/plant_state', self.power_measurement, feedback_qos)
        self.create_subscription(Float64MultiArray, '/hil_hitch/plant_state', self.measurement, feedback_qos)
        self.create_subscription(JointState, '/joint_states', self.wheels, 10)
        from rclpy.clock import Clock, ClockType
        self.create_timer(.02, self.tick, clock=Clock(clock_type=ClockType.STEADY_TIME))
        self.get_logger().info('Dedicated hitch plant '+('SIL' if args.sil else 'PHYSICAL CAN')+'; real chassis IDs ignored')

    def measurement(self, msg):
        if len(msg.data) != 9 or not all(math.isfinite(v) for v in msg.data):
            return
        self.state, self.state_at = list(msg.data), time.monotonic()

    def wheels(self, msg):
        angles = [p for n, p in zip(msg.name, msg.position) if n in
                  ('front_left_steering_joint', 'front_right_steering_joint') and math.isfinite(p)]
        if angles:
            self.steering = sum(angles)/len(angles)

    def power_measurement(self, msg):
        try:
            sensors(msg.data, 0)
        except (ValueError, OverflowError, struct.error):
            return
        self.power_state = list(msg.data); self.power_at = time.monotonic()

    def send(self, identity, data):
        self.transmitting = True
        self.bus.send(FRAME.pack(identity, 8, data))
        if self.tx_guard is not None:
            self.tx_guard.sent()
        self.transmitting = False
        self.trace.write(json.dumps(dict(at=time.monotonic(), direction='tx', id=identity, data=data.hex()))+'\n')
        time.sleep(.001)

    def hold(self):
        self.drive_pub.publish(Twist())
        self.valve_pub.publish(Int16(data=0))
        self.power_pub.publish(Float64MultiArray(data=[0., 0., 0., 640., 0., 0., 0., 0., 1.]))

    def tick(self):
        now = time.monotonic()
        try:
            if self.tx_guard is not None:
                self.transmitting = True
                self.tx_guard.check()
                self.transmitting = False
            self.tick_io()
        except OSError as error:
            self.hold()
            if self.tx_guard is None or not self.transmitting or not temporary_tx_error(error):
                raise
            if not self.fault:
                self.fault_since = now
            self.fault, self.recoverable_fault = str(error), True
            self.errors += 1
            self.get_logger().error(self.fault, throttle_duration_sec=2.)
            if now-self.fault_since >= 1.:
                self.bus, reopened = self.tx_guard.recover(self.bus, self.open_bus)
                if reopened:
                    self.drive, self.valve = (0., 0., False), 0
                    self.drive_lease, self.valve_lease = Lease(), Lease()
                    self.power_pair = ActuatorPair()
                    self.awaiting_neutral, self.last_feedback_sent = True, 0.
                    self.get_logger().info('Reopened hitch CAN; waiting for fresh neutral command and physical feedback')
        if now-self.last_status >= 1.:
            self.status_pub.publish(String(data=json.dumps(dict(fault=self.fault, errors=self.errors,
                recoverable_fault=self.recoverable_fault, awaiting_neutral=self.awaiting_neutral,
                transport_recoveries=self.tx_guard.recoveries if self.tx_guard is not None else 0,
                feedback_fresh=now-self.last_feedback_sent < .2))))
            self.last_status = now

    def tick_io(self):
        now = time.monotonic()
        for _ in range(64):
            try:
                raw = self.bus.recv(FRAME.size)
            except BlockingIOError:
                break
            identity, length, data = FRAME.unpack(raw)
            if length != 8 or identity not in (DRIVE, VALVE, ENGINE, TRANSMISSION):
                continue
            try:
                if identity == DRIVE:
                    drive = decode_drive(data)
                    if self.drive_lease.accept(data, now, not drive[2]):
                        self.drive = drive
                        if not drive[2]:
                            self.awaiting_neutral = False
                elif identity == VALVE:
                    value, enabled = decode_valve(data)
                    if self.valve_lease.accept(data, now, value == 0):
                        self.valve = value if enabled else 0
                else:
                    self.power_pair.accept(identity, data, now)
                self.trace.write(json.dumps(dict(at=now, direction='rx', id=identity, data=data.hex()))+'\n')
            except ValueError as error:
                self.trace.write(json.dumps(dict(at=now, direction='reject', id=identity, error=str(error)))+'\n')
        drive = self.drive if (self.drive_lease.fresh(now) and not self.fault and
            not self.awaiting_neutral and now-self.last_feedback_sent < .2) else (0., 0., False)
        velocity = Twist()
        if drive[2]:
            velocity.linear.x = drive[0]
            velocity.angular.z = drive[0]*math.tan(drive[1])/2.1851241912748174
        self.drive_pub.publish(velocity)
        self.valve_pub.publish(Int16(data=self.valve if self.valve_lease.fresh(now) and
            not self.fault and not self.awaiting_neutral else 0))
        self.power_pub.publish(Float64MultiArray(data=self.power_pair.current(now) if
            not self.fault and not self.awaiting_neutral else
            [0., 0., 0., 640., 0., 0., 0., 0., 1.]))
        fresh = self.state is not None and now-self.state_at < .15
        values = self.state if fresh else [0.]*9
        # Real plant measurements must continue during neutral priming: the ECU
        # needs them before it can forward the X5's new neutral commands.
        self.send(SENSOR, sensor_feedback(values[0], values[1], int(not fresh), self.counter))
        self.send(DRIVE_STATE, seal(struct.pack('<hhBBB', round(max(-32., min(32., values[4]))*1000),
                                               round(self.steering*10000), int(fresh), int(not fresh), self.counter)))
        if self.power_state is not None:
            for identity, data in zip(SENSORS, sensors(self.power_state, self.counter,
                                                      int(now-self.power_at > .15))):
                self.send(identity, data)
        self.last_feedback_sent = now
        if (self.recoverable_fault and not self.awaiting_neutral and
                self.tx_guard.last_progress_at > self.fault_since):
            self.fault, self.recoverable_fault = '', False
            self.drive, self.valve = (0., 0., False), 0
            self.get_logger().info('Physical hitch CAN recovered; old actuator targets discarded')
        self.counter = (self.counter+1) & 255
        self.trace.flush()

    def destroy_node(self):
        if self.context.ok():
            self.drive_pub.publish(Twist())
            self.valve_pub.publish(Int16(data=0))
            self.power_pub.publish(Float64MultiArray(data=[0., 0., 0., 640., 0., 0., 0., 0., 1.]))
        self.bus.close(); self.trace.close()
        super().destroy_node()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--interface', default='can0')
    parser.add_argument('--serial', default='003000224856511520343739')
    parser.add_argument('--sil', action='store_true')
    parser.add_argument('--powertrain', action='store_true')
    parser.add_argument('--trace', type=Path, required=True)
    args, ros = parser.parse_known_args()
    args.trace.parent.mkdir(parents=True, exist_ok=True)
    rclpy.init(args=ros)
    node = Bridge(args)
    try:
        rclpy.spin(node)
    except (KeyboardInterrupt, rclpy.executors.ExternalShutdownException):
        pass
    finally:
        node.destroy_node()
        rclpy.try_shutdown()
