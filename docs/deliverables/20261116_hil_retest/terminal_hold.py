"""Observe five simulation seconds after task completion, without publishing."""
import argparse
import json
import math
from pathlib import Path
import time

from action_msgs.msg import GoalStatusArray
from nav_msgs.msg import Odometry
import rclpy
from rclpy.node import Node
from rclpy.qos import qos_profile_action_status_default, qos_profile_sensor_data
from rosgraph_msgs.msg import Clock


def evaluate(car, goal, samples, span, planner_goals, new_goals, errors):
    tolerance = {'Car0': .2632937431, 'Car3': .4185015549, 'Car4': 1.0925620956}[car]
    speed_limit = .03 if car == 'Car4' else .02
    maximum_speed = max((math.hypot(*s['linear_velocity'][:2]) for s in samples), default=None)
    final_error = math.dist(samples[-1]['xy'], goal) if samples else None
    ticks = sorted(s['clock'] for s in samples)
    coverage = ticks[-1]-ticks[0] if ticks else 0.
    max_gap = max((b-a for a, b in zip(ticks, ticks[1:])), default=None)
    return dict(passed=bool(not errors and len(samples) >= 3 and span >= 5. and coverage >= 4.8
                           and max_gap <= .25 and maximum_speed <= speed_limit
                           and final_error <= tolerance and planner_goals and not new_goals),
        final_error_m=final_error, half_wheelbase_m=tolerance, maximum_planar_speed_mps=maximum_speed,
        speed_limit_mps=speed_limit, sample_coverage_sim_seconds=coverage, maximum_sample_gap_sim_seconds=max_gap)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--run', required=True)
    parser.add_argument('--car', choices=('Car0', 'Car3', 'Car4'), required=True)
    parser.add_argument('--goal', nargs=2, type=float, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    state_path = args.root/'.local/hil/state.json'

    def check_state():
        state = json.loads(state_path.read_text())
        if state.get('run_id') != args.run or state.get('phase') != 'completed':
            raise RuntimeError('The selected completed HIL session is no longer active')

    check_state()
    rclpy.init()
    node = Node('hil_terminal_passive_observer')
    clock = [None]
    samples, goals, errors = [], {}, []
    first = None
    planned_after_start = set()

    def receive_pose(msg):
        stamp = msg.header.stamp.sec+msg.header.stamp.nanosec*1e-9
        p, v = msg.pose.pose.position, msg.twist.twist.linear
        samples.append(dict(stamp=stamp, clock=clock[0], xy=[p.x, p.y],
                            linear_velocity=[v.x, v.y, v.z], yaw_rate=msg.twist.twist.angular.z))

    def receive_status(name, msg):
        identities = {bytes(s.goal_info.goal_id.uuid).hex() for s in msg.status_list}
        if name not in goals:
            goals[name] = identities
            return
        planned_after_start.update((name, identity) for identity in identities-goals[name])
        goals[name].update(identities)

    node.create_subscription(Clock, '/clock', lambda m: clock.__setitem__(0, m.clock.sec+m.clock.nanosec*1e-9), qos_profile_sensor_data)
    truth = '/hil_hitch/ground_truth' if args.car == 'Car4' else '/hil/ground_truth'
    node.create_subscription(Odometry, truth, receive_pose, qos_profile_sensor_data)
    for name in ('/compute_path_to_pose', '/compute_path_through_poses'):
        node.create_subscription(GoalStatusArray, name+'/_action/status',
            lambda m, name=name: receive_status(name, m), qos_profile_action_status_default)
    began = time.monotonic()
    try:
        while time.monotonic()-began < 90.:
            check_state()
            rclpy.spin_once(node, timeout_sec=.05)
            if clock[0] is not None and samples:
                if first is None:
                    first = clock[0]
                if clock[0]-first >= 5.:
                    break
        else:
            raise TimeoutError('Five advancing simulation seconds were not observed')
    except Exception as error:
        errors.append(str(error))
    finally:
        node.destroy_node()
        rclpy.try_shutdown()
    window = [s for s in samples if s['clock'] is not None and first is not None and s['clock'] >= first]
    # Gazebo odometry reports world-frame linear velocity. Its planar norm is
    # conservative for the requirement on longitudinal speed at rest.
    span = clock[0]-first if clock[0] is not None and first is not None else 0.
    report = dict(scope='Passive terminal-window observations, not all NAV-j branches', run_id=args.run,
        **evaluate(args.car, args.goal, window, span, goals, planned_after_start, errors),
        simulation_seconds=span, samples=window, goal=args.goal,
        observed_planner_actions={k: sorted(v) for k, v in goals.items()},
        new_planner_goals=sorted(planned_after_start), errors=errors)
    args.output.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps({k: v for k, v in report.items() if k != 'samples'}), flush=True)
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
