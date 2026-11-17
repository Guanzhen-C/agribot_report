"""Bounded Car0 client rejection checks using actual feedback and MoveIt services."""
import argparse
import json
from pathlib import Path
import time

import rclpy
from geometry_msgs.msg import Twist
from rclpy.clock import Clock, ClockType
from rclpy.qos import QoSProfile, DurabilityPolicy
from sensor_msgs.msg import JointState
from std_msgs.msg import Bool
from trajectory_msgs.msg import JointTrajectory
from moveit_msgs.srv import GetPositionIK

from agribot_hil_apple.arm_preflight import JOINTS, FINGERS
from agribot_hil_apple.harvest import Harvester, pose


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scenario', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    rclpy.init(args=['--ros-args', '-r', '__node:=hil_arm_negative_checks'])
    node = Harvester(args.scenario, args.output/'client-events.json')
    speed = [0.]
    command_count = [0]
    drive = node.create_publisher(Twist, '/hil/navigation/cmd_vel', 1)
    def renew():
        msg = Twist()
        msg.linear.x = speed[0]
        drive.publish(msg)
    node.create_timer(.05, renew, clock=Clock(clock_type=ClockType.STEADY_TIME))
    node.create_subscription(JointTrajectory, '/hil/arm/command',
                             lambda _: command_count.__setitem__(0, command_count[0]+1), 20)
    report = dict(passed=False, scope='Real stopped/moving HIL feedback and actual MoveIt refusal; no arm execution request',
                  checks=[], requested_arm_trajectories=0)
    def pump(seconds, until=None):
        end = time.monotonic()+seconds
        while time.monotonic() < end:
            rclpy.spin_once(node, timeout_sec=.02)
            if until is not None and until():
                return True
        return False
    def check(name, passed, **details):
        report['checks'].append(dict(name=name, passed=bool(passed), **details))
        if not passed:
            raise AssertionError(name)
    def rejects(name, function, fragment):
        try:
            function()
        except (RuntimeError, TimeoutError, KeyError) as error:
            check(name, fragment in str(error), rejection=str(error))
        else:
            check(name, False, error='Unexpected acceptance')
    qos = QoSProfile(depth=1, durability=DurabilityPolicy.TRANSIENT_LOCAL)
    try:
        node.wait_observation()
        node.transit_hold(True)
        # Lose feedback only for this test client, not the real publisher or plant.
        subscriptions = [s for s in node.subscriptions if s.topic_name == '/hil/gripper/transit_hold/status']
        check('actual_mode_feedback_subscription_present', len(subscriptions) == 1)
        node.destroy_subscription(subscriptions[0])
        rejects('unconfirmed_mode_switch_refuses', lambda: node.transit_hold(False), 'did not acknowledge')
        node.create_subscription(Bool, '/hil/gripper/transit_hold/status', node.receive_transit_status, qos)
        node.transit_hold(True)
        node.wait_observation()
        joints = [s for s in node.subscriptions if s.topic_name == '/joint_states']
        check('actual_joint_feedback_subscription_present', len(joints) == 1)
        node.destroy_subscription(joints[0])
        pump(1.6)
        rejects('stale_joint_feedback_refuses', node.wait_observation, 'Fresh advancing')
        node.create_subscription(JointState, '/joint_states', node.receive_joints, 20)
        node.wait_observation()
        start = node.state()
        request = GetPositionIK.Request()
        request.ik_request.group_name = 'arm'
        request.ik_request.ik_link_name = 'manipulator_tcp'
        request.ik_request.robot_state = start
        request.ik_request.pose_stamped.header.frame_id = 'base_link'
        request.ik_request.pose_stamped.pose = pose([100., 100., 100.])
        request.ik_request.timeout.sec = 1
        request.ik_request.avoid_collisions = True
        report['response_timeout_fixture_seconds'] = .001
        rejects('planning_response_timeout_refuses',
                lambda: node.call('/compute_ik', GetPositionIK, request, timeout=.001), '/compute_ik')
        rejects('unreachable_collision_checked_ik_refuses',
                lambda: node.ik([100., 100., 100.], [0., 0., 0., 1.], start), 'IK unavailable')
        rejects('incomplete_cartesian_path_refuses',
                lambda: node.cartesian(start, pose([100., 100., 100.])), 'Incomplete collision-checked')
        bad = node.state()
        for index in range(6):
            bad.joint_state.position[index] = 100.
        rejects('invalid_start_state_refuses', lambda: node.plan(bad, node.state()), 'OMPL planning failed')
        speed[0] = .12
        moving = pump(15., lambda: node.observation is not None and node.observation['base_speed'] > .05)
        check('actual_chassis_moves_before_interlock_test', moving,
              base_speed=node.observation.get('base_speed') if node.observation else None)
        rejects('moving_chassis_refuses_manipulation', node.wait_observation, 'Vehicle must be stopped')
        speed[0] = 0.
        node.wait_observation()
        check('new_fresh_stopped_feedback_recovers',
              node.observation['base_speed'] <= .035 and abs(node.observation['base_yaw_rate']) <= .05)
        check('no_arm_trajectory_was_published', command_count[0] == 0)
        report['passed'] = True
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        speed[0] = 0.
        try:
            pump(1.)
            if not any(s.topic_name == '/hil/gripper/transit_hold/status' for s in node.subscriptions):
                node.create_subscription(Bool, '/hil/gripper/transit_hold/status', node.receive_transit_status, qos)
            node.transit_hold(True)
        except BaseException as error:
            report.update(passed=False, cleanup_error=str(error))
            raise
        finally:
            report['requested_arm_trajectories'] = command_count[0]
            (args.output/'result.json').write_text(json.dumps(report, indent=2)+'\n')
            node.destroy_node()
            rclpy.try_shutdown()
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
