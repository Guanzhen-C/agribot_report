"""Check the real harvester's request guard during an observed fruit attachment.

This additional client never controls the primary mission. Its publishing proxy
fails closed: an unexpected publish is a failed guard test, not a physical open.
"""
import argparse
import json
from pathlib import Path
import time

import rclpy
from moveit_msgs.msg import CollisionObject, PlanningScene
from rclpy.qos import qos_profile_sensor_data

from agribot_hil_apple.arm_preflight import FINGERS
from agribot_hil_apple.harvest import Harvester


class NoPublish:
    def __init__(self):
        self.attempts = 0

    def publish(self, _):
        self.attempts += 1
        raise AssertionError('Held-fruit guard attempted to publish a mode change')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scenario', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    rclpy.init(args=['--ros-args', '-r', '__node:=hil_held_fruit_request_guard_check'])
    node = Harvester(args.scenario, args.output.with_suffix('.events.json'))
    recorder = NoPublish()
    node.transit_publisher = recorder
    report = dict(passed=False, hardware_outputs=False,
        scope='Actual harvester client rejection, passive MoveIt attachment and physical fingers; no scene-query RPCs; publishing fails closed',
        observations=[])
    attached = set()
    scene_received = [0.]

    def scene(message):
        if not message.is_diff:
            attached.clear()
        for obj in message.robot_state.attached_collision_objects:
            identity = obj.object.id
            if not identity.startswith('harvest_apple_'):
                continue
            if obj.object.operation == CollisionObject.REMOVE or obj.link_name != 'manipulator_gripper_shell':
                attached.discard(identity)
            else:
                attached.add(identity)
        scene_received[0] = time.monotonic()

    node.create_subscription(PlanningScene, '/monitored_planning_scene', scene, qos_profile_sensor_data)

    try:
        deadline = time.monotonic()+1200.
        while time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=.05)
            if node.observation is None or not all(name in node.joints for name in FINGERS):
                continue
            if time.monotonic()-node.received > 1.5 or time.monotonic()-node.joint_receipt > 1.5:
                continue
            if time.monotonic()-scene_received[0] > .5:
                continue
            before = set(attached)
            if not before:
                rclpy.spin_once(node, timeout_sec=.15)
                continue
            model = sorted(before)[0]
            fingers = [node.joints[name] for name in FINGERS]
            # Use the actually observed attached ID, never invent a held state.
            node.report['held_fruit'] = model
            try:
                node.transit_hold(True)
            except RuntimeError as error:
                rejection = str(error)
                if 'while holding fruit' not in rejection:
                    raise
            else:
                raise AssertionError('A held-fruit transit request was accepted')
            until = time.monotonic()+.2
            while time.monotonic() < until:
                rclpy.spin_once(node, timeout_sec=.02)
            after = set(attached)
            sample = dict(model=model, before=sorted(before), after=sorted(after),
                sim_time=node.get_clock().now().nanoseconds/1e9, fingers_before=fingers,
                fingers_after=[node.joints[name] for name in FINGERS], rejection=rejection,
                publish_attempts=recorder.attempts)
            report['observations'].append(sample)
            if model not in after:
                continue
            report['passed'] = (recorder.attempts == 0 and
                max(abs(value) for value in sample['fingers_after']) < .004)
            if not report['passed']:
                raise AssertionError('Physical fingers or held-fruit request guard failed')
            break
        else:
            raise TimeoutError('No stable actual held-fruit observation during the full mission')
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        args.output.write_text(json.dumps(report, indent=2)+'\n')
        node.destroy_node()
        rclpy.try_shutdown()
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
