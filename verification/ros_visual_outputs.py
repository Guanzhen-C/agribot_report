#!/usr/bin/env python3
"""Observe real simulated-camera inference without publishing motion commands."""
import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--domain', type=int, default=37)
    parser.add_argument('--timeout', type=float, default=60)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    os.environ['ROS_DOMAIN_ID'] = str(args.domain)
    os.environ['ROS_LOCALHOST_ONLY'] = '1'
    import numpy as np
    import rclpy
    from rclpy.qos import qos_profile_sensor_data
    from sensor_msgs.msg import Image
    from std_msgs.msg import String
    from ament_index_python.packages import get_package_prefix

    python = os.environ['AGRIBOT_VISION_PYTHON']
    models = Path(os.environ['AGRIBOT_VISION_MODEL_DIR'])
    executable = Path(get_package_prefix('agribot_visual_perception')) / 'lib/agribot_visual_perception/visual_perception_node'
    if not Path(python).is_file() or not executable.is_file():
        raise RuntimeError('Install the repository vision environment and ROS packages first')
    args.output.mkdir(parents=True, exist_ok=True)
    if (args.output / 'result.json').exists():
        raise RuntimeError('Choose a new output directory to preserve the previous evidence')
    rclpy.init()
    node = rclpy.create_node('delivery_visual_observer')
    images, results, statuses = {}, {}, {}

    def stamp(header):
        return header.stamp.sec, header.stamp.nanosec

    def image(message):
        pixels = np.frombuffer(message.data, dtype=np.uint8)
        images[stamp(message.header)] = dict(width=message.width, height=message.height,
            frame=message.header.frame_id, pixel_std=float(pixels.std()), encoding=message.encoding)
        if len(images) > 128:
            images.pop(next(iter(images)))

    def result(message):
        data = json.loads(message.data)
        key = data['stamp']['sec'], data['stamp']['nanosec']
        results.setdefault(data['mode'], {})[key] = data

    def status(message):
        data = json.loads(message.data)
        statuses[data['mode']] = data

    node.create_subscription(Image, '/vision/annotated_image', image, qos_profile_sensor_data)
    node.create_subscription(String, '/vision/results', result, 10)
    node.create_subscription(String, '/vision/status', status, 10)
    evidence = dict(domain=args.domain, input='/camera/rgb/image_raw', cases=[])
    try:
        for mode in ('object_detection', 'instance_segmentation', 'pose_estimation'):
            images.clear()
            name = 'delivery_visual_' + mode
            command = [python, str(executable), '--ros-args', '-r', '__node:=' + name,
                '-p', 'use_sim_time:=true', '-p', 'mode:=' + mode,
                '-p', 'model_dir:=' + str(models), '-p', 'device:=cpu']
            with (args.output / (mode + '.log')).open('w') as log:
                process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
                try:
                    deadline = time.monotonic() + args.timeout
                    while time.monotonic() < deadline:
                        rclpy.spin_once(node, timeout_sec=.1)
                        if process.poll() is not None:
                            raise RuntimeError(mode + ' exited before producing camera output')
                        matched = sorted(set(images) & set(results.get(mode, {})))
                        metric = statuses.get(mode, {})
                        if len(matched) >= 3 and metric.get('processed_frames', 0) >= 3:
                            break
                    else:
                        raise RuntimeError(mode + ' did not produce three timestamp-matched outputs')
                    sample = images[matched[-1]]
                    if not sample['frame'] or sample['width'] < 32 or sample['height'] < 32 or sample['pixel_std'] < 1:
                        raise RuntimeError(mode + ' produced a blank or invalid image')
                    publishers = node.get_publisher_names_and_types_by_node(name, '/')
                    if metric.get('motion_control_connected') is not False or any('cmd_vel' in topic for topic, _ in publishers):
                        raise RuntimeError('Visual observation unexpectedly publishes motion commands')
                    evidence['cases'].append(dict(mode=mode, status='PASS', matched_outputs=len(matched),
                        image=sample, processing=metric, publishers=publishers,
                        objects_in_last_frame=len(results[mode][matched[-1]]['objects'])))
                    (args.output / 'result.json').write_text(json.dumps(evidence, indent=2) + '\n')
                    print(mode + ': PASS (real camera input, timestamp-matched ROS outputs)', flush=True)
                finally:
                    if process.poll() is None:
                        os.killpg(process.pid, signal.SIGINT)
                        try:
                            process.wait(timeout=10)
                        except subprocess.TimeoutExpired:
                            os.killpg(process.pid, signal.SIGKILL)
                            process.wait(timeout=5)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
