"""Record stamped truth, localization and paths without providing any control input."""
import argparse
import bisect
import json
import math
from pathlib import Path
import time

import numpy as np


def distances_to_path(points, path):
    points, path = np.asarray(points, dtype=float), np.asarray(path, dtype=float)
    if len(path) < 2:
        raise ValueError('A path requires at least two points')
    start, delta = path[:-1], np.diff(path, axis=0)
    length2 = np.einsum('ij,ij->i', delta, delta)
    result = []
    for point in points:
        ratios = np.divide(np.einsum('ij,ij->i', point-start, delta), length2,
                           out=np.zeros(len(delta)), where=length2 > 0)
        nearest = start+np.clip(ratios, 0, 1)[:, None]*delta
        result.append(float(np.linalg.norm(nearest-point, axis=1).min()))
    return result


def summary(values):
    return dict(samples=len(values), p95_m=float(np.percentile(values, 95)) if values else None,
                maximum_m=max(values) if values else None)


def analyze(records):
    truth = sorted((r for r in records if r['kind'] == 'truth'), key=lambda r: r['stamp'])
    # Multiple messages at the same simulation stamp are retained in raw input;
    # interpolation uses the last observation at that exact epoch.
    by_stamp = {r['stamp']: r for r in truth}
    epochs = sorted(by_stamp)
    localization, matches, unmatched = [], [], []
    for row in (r for r in records if r['kind'] == 'localization'):
        stamp = row['stamp']
        index = bisect.bisect_left(epochs, stamp)
        if index < len(epochs) and epochs[index] == stamp:
            a = b = by_stamp[stamp]
        elif 0 < index < len(epochs):
            a, b = by_stamp[epochs[index-1]], by_stamp[epochs[index]]
        else:
            unmatched.append(dict(stamp=stamp, reason='No truth bracket'))
            continue
        gap = b['stamp']-a['stamp']
        if gap > .15:
            unmatched.append(dict(stamp=stamp, reason='Truth gap exceeds 0.15 simulation seconds', gap=gap))
            continue
        ratio = 0. if gap == 0 else (stamp-a['stamp'])/gap
        actual = np.asarray(a['xy'])+ratio*(np.asarray(b['xy'])-a['xy'])
        error = float(np.linalg.norm(actual-row['xy']))
        localization.append(error)
        matches.append(dict(stamp=stamp, truth_xy=actual.tolist(), localized_xy=row['xy'],
                            xy_error_m=error, bracket_sim_seconds=gap, ready=row.get('ready')))
    paths = sorted((r for r in records if r['kind'] == 'path'), key=lambda r: r['stamp'])
    path_epochs = [r['stamp'] for r in paths]
    tracking = []
    for row in truth:
        index = bisect.bisect_right(path_epochs, row['stamp'])-1
        if index < 0:
            continue
        path = paths[index]
        if len(path['xy']) < 2:
            continue
        distance = distances_to_path([row['xy']], path['xy'])[0]
        tracking.append(dict(stamp=row['stamp'], path_stamp=path['stamp'], xy_error_m=distance))
    return dict(scope='Stamped independent pose/path measurements; no agronomic accuracy claim',
        time_basis='Odometry/Path header simulation stamps, never cross-host wall-clock subtraction',
        truth_messages=len(truth), localization_messages=len(matches)+len(unmatched), paths=len(paths),
        localization_xy=summary(localization),
        localization_when_ready=summary([r['xy_error_m'] for r in matches if r['ready'] is True]),
        localization_match_coverage=len(matches)/(len(matches)+len(unmatched)) if matches or unmatched else 0.,
        maximum_truth_interpolation_gap_sim_seconds=max((r['bracket_sim_seconds'] for r in matches), default=None),
        path_tracking_xy=summary([r['xy_error_m'] for r in tracking]),
        unmatched_localization=unmatched, matched_localization=matches, path_tracking_samples=tracking,
        limitation='Nearest distance to the latest published Smac polyline; endpoint acceptance is independently checked by terminal_hold.py.')


def record(args):
    import rclpy
    from rclpy.node import Node
    from rclpy.qos import qos_profile_sensor_data
    from nav_msgs.msg import Odometry, Path as NavPath
    from std_msgs.msg import Bool
    from agribot_hil_apple.async_trace import AsyncTrace
    rclpy.init()
    node = Node('hil_stamped_pose_readonly_audit')
    stream = AsyncTrace(args.output)
    ready = [args.car == 'Car4']
    def stamp(message):
        return message.header.stamp.sec+message.header.stamp.nanosec*1e-9
    def receive(kind, message):
        p = message.pose.pose.position
        q, v = message.pose.pose.orientation, message.twist.twist
        row = dict(kind=kind, at=time.monotonic(), stamp=stamp(message), frame=message.header.frame_id,
                   child_frame=message.child_frame_id, xy=[p.x, p.y], z=p.z, ready=ready[0],
                   quaternion=[q.x, q.y, q.z, q.w], velocity=[v.linear.x, v.linear.y, v.linear.z],
                   angular_z=v.angular.z)
        stream.write(json.dumps(row)+'\n')
    truth = '/hil_hitch/ground_truth' if args.car == 'Car4' else '/hil/ground_truth'
    fused = '/odometry/filtered_navsat' if args.car == 'Car4' else '/fastlivo_rtk/odometry'
    node.create_subscription(Odometry, truth, lambda m: receive('truth', m), qos_profile_sensor_data)
    node.create_subscription(Odometry, fused, lambda m: receive('localization', m), qos_profile_sensor_data)
    node.create_subscription(Bool, '/fastlivo_rtk/ready', lambda m: ready.__setitem__(0, m.data), 1)
    node.create_subscription(NavPath, '/plan', lambda m: stream.write(json.dumps(dict(kind='path',
        at=time.monotonic(), stamp=stamp(m), frame=m.header.frame_id,
        xy=[[p.pose.position.x, p.pose.position.y] for p in m.poses]))+'\n'), qos_profile_sensor_data)
    try:
        deadline = time.monotonic()+2400.
        while rclpy.ok() and time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=.1)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        stream.close()
        rclpy.try_shutdown()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--car', choices=('Car0', 'Car3', 'Car4'))
    parser.add_argument('--analyze', type=Path)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    if args.analyze:
        rows = [json.loads(s) for s in args.analyze.read_text().splitlines()]
        result = analyze(rows)
        args.output.write_text(json.dumps(result, indent=2)+'\n')
        print(json.dumps({k: v for k, v in result.items() if k not in
                          ('matched_localization', 'unmatched_localization', 'path_tracking_samples')}, indent=2))
        if not result['localization_xy']['samples'] or not result['path_tracking_xy']['samples']:
            raise SystemExit('Incomplete pose/path measurement coverage')
    elif args.car:
        record(args)
    else:
        parser.error('Choose --car for recording or --analyze for offline measurements')


if __name__ == '__main__':
    main()
