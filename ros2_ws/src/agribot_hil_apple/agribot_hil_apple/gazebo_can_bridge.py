"""Native CAN is the only command source for the simulated chassis."""
import errno
import json
import math
import time
from pathlib import Path

import rclpy
from geometry_msgs.msg import Twist
from nav_msgs.msg import Odometry
from rclpy.clock import Clock, ClockType
from rclpy.node import Node
from rcl_interfaces.msg import SetParametersResult
from std_msgs.msg import String

from .can_wire import CanSocket, COMMAND_ID, FEEDBACK_IDS, decode_command, feedback_payload


def actuator_command(payload, wheelbase, authorized):
    speed, steering = decode_command(payload)
    if not math.isfinite(wheelbase) or wheelbase <= 0:
        raise ValueError('Invalid wheelbase')
    if abs(speed) > .81 or abs(steering) > .385:
        raise ValueError('Command exceeds the physical Car0 limits')
    return (speed, speed * math.tan(steering) / wheelbase) if authorized else (0., 0.)


class GazeboCanBridge(Node):
    def __init__(self):
        super().__init__('hil_gazebo_can_bridge')
        self.authorized = self.declare_parameter('motion_authorized', False).value
        serial = self.declare_parameter('usb_serial', '').value.removeprefix('usb:')
        interface = self.declare_parameter('can_interface', 'can0').value
        self.wheelbase = self.declare_parameter('wheelbase_m', .5265874862670898).value
        self.bus = CanSocket(interface, serial, [COMMAND_ID])
        self.bus.socket.setblocking(False)
        self.publisher = self.create_publisher(Twist, '/hil/actuator/cmd_vel', 1)
        self.status = self.create_publisher(String, '/hil/can/status', 1)
        self.create_subscription(Odometry, '/hil/ground_truth', self.odometry, 5)
        self.last_command = self.last_truth = 0.
        self.speed = self.yaw_rate = 0.
        self.target = (0., 0.)
        self.awaiting_neutral = True
        self.rejected_until_neutral = 0
        self.rx = self.tx = self.errors = 0
        self.nonzero_rx = 0
        self.last_feedback = self.last_status = 0.
        self.last_feedback_sent = 0.
        self.fault = ''
        self.recoverable_fault = False
        self.last_recovery_attempt = -math.inf
        self.transport_recoveries = 0
        self.add_on_set_parameters_callback(self.configure_motion)
        self.timer = self.create_timer(.01, self.tick, clock=Clock(clock_type=ClockType.STEADY_TIME))

    def configure_motion(self, parameters):
        for parameter in parameters:
            if parameter.name in ('usb_serial', 'can_interface', 'wheelbase_m'):
                if parameter.value != self.get_parameter(parameter.name).value:
                    return SetParametersResult(successful=False, reason='Physical CAN configuration requires a restart')
        for parameter in parameters:
            if parameter.name == 'motion_authorized':
                self.authorized = bool(parameter.value)
                self.target = (0., 0.)
                self.last_command = 0.
                self.awaiting_neutral = True
        return SetParametersResult(successful=True)

    def odometry(self, msg):
        self.speed, self.yaw_rate = msg.twist.twist.linear.x, msg.twist.twist.angular.z
        self.last_truth = time.monotonic()

    def tick(self):
        now = time.monotonic()
        transmitting_feedback = False
        try:
            transmitting_feedback = True
            self.bus.check_transmitter()
            transmitting_feedback = False
            if now-self.last_command >= .3:
                self.target = (0., 0.)
                self.awaiting_neutral = True
            for _ in range(64):
                frame = self.bus.receive()
                if frame is None:
                    break
                identifier, data = frame
                if identifier != COMMAND_ID:
                    raise ValueError('Unexpected CAN command ID')
                target = actuator_command(data, self.wheelbase, True)
                self.nonzero_rx += int(abs(decode_command(data)[0]) > .0001)
                self.rx += 1
                # A resumed sender can drain an old nonzero packet before its
                # current zero. Expiry must stay latched through that packet.
                if self.awaiting_neutral and target != (0., 0.):
                    self.rejected_until_neutral += 1
                    continue
                self.awaiting_neutral = False
                self.target = target if self.authorized else (0., 0.)
                self.last_command = now
            enabled = (self.authorized and now - self.last_truth < .5 and not self.fault
                       and now - self.last_feedback_sent < .5 and not self.awaiting_neutral)
            command = Twist()
            if enabled and now - self.last_command < .3:
                command.linear.x, command.angular.z = self.target
            self.publisher.publish(command)
            if (now - self.last_feedback >= .05 and now - self.last_truth < .5
                    and now - self.last_command < .5):
                payload = feedback_payload(self.speed, self.yaw_rate,
                                           angular_velocity=(0., 0., self.yaw_rate),
                                           stopped=not enabled)
                # Bound retries too: partial sends must not turn 20 Hz feedback
                # into a 100 Hz retry storm against an already full CAN queue.
                self.last_feedback = now
                transmitting_feedback = True
                for index, identifier in enumerate(FEEDBACK_IDS):
                    self.bus.send(identifier, payload[index * 8:index * 8 + 8])
                    self.tx += 1
                    if index < len(FEEDBACK_IDS)-1:
                        # Match the verified finite probe: the CDC/6750 path
                        # must not receive all three fragments as one USB burst.
                        time.sleep(.003)
                self.last_feedback_sent = now
                if self.recoverable_fault:
                    self.fault = ''
                    self.recoverable_fault = False
                    self.target = (0., 0.)
                    self.last_command = 0.
                    self.awaiting_neutral = True
                    self.get_logger().info('CAN feedback recovered; waiting for native zero handshake')
        except Exception as error:
            transient = (transmitting_feedback and isinstance(error, OSError)
                         and error.errno in (errno.ENOBUFS, errno.EAGAIN, errno.EWOULDBLOCK))
            # A later queue-full error must never clear or downgrade a real
            # transport/protocol fault. Only temporary TX backpressure recovers.
            if not self.fault or self.recoverable_fault:
                self.fault = str(error)
                self.recoverable_fault = transient
            self.errors += 1
            self.publisher.publish(Twist())
            self.get_logger().error(self.fault, throttle_duration_sec=2.)
            if (self.recoverable_fault and now-self.last_feedback_sent >= 1.
                    and now-self.last_recovery_attempt >= 5.):
                self.last_recovery_attempt = now
                self.target = (0., 0.)
                self.last_command = self.last_feedback_sent = 0.
                self.awaiting_neutral = True
                try:
                    if self.transport_recoveries >= 3:
                        raise RuntimeError('Repeated CAN transmitter stalls; inspect the physical adapter')
                    if self.bus.recover_stalled_transmitter():
                        self.transport_recoveries += 1
                        self.get_logger().info('Reopened stalled CAN transport; holding zero until fresh command and complete feedback')
                except Exception as recovery_error:
                    self.fault = 'CAN transport recovery failed: '+str(recovery_error)
                    self.recoverable_fault = False
                    self.get_logger().error(self.fault)
        if now - self.last_status > 1.:
            self.status.publish(String(data=json.dumps({
                'authorized': self.authorized, 'rx_commands': self.rx,
                'nonzero_commands': self.nonzero_rx,
                'tx_feedback': self.tx, 'errors': self.errors, 'fault': self.fault,
                'recoverable_fault': self.recoverable_fault,
                'transport_recoveries': self.transport_recoveries,
                'awaiting_neutral': self.awaiting_neutral,
                'rejected_until_neutral': self.rejected_until_neutral,
                'feedback_fresh': now - self.last_feedback_sent < .5,
                'command_fresh': now - self.last_command < .3,
                'truth_fresh': now - self.last_truth < .5})))
            self.last_status = now

    def destroy_node(self):
        self.publisher.publish(Twist())
        self.bus.close()
        super().destroy_node()


def main():
    rclpy.init()
    node = GazeboCanBridge()
    try:
        rclpy.spin(node)
    except (KeyboardInterrupt, rclpy.executors.ExternalShutdownException):
        pass
    finally:
        node.destroy_node()
        rclpy.try_shutdown()
