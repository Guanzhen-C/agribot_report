"""Observe actual implement rest state after completion, without publishers or CAN I/O."""
import argparse
import json
from pathlib import Path
import time


def evaluate(car, samples):
    if len(samples) < 3 or samples[-1]['sim'] - samples[0]['sim'] < 4.8:
        raise ValueError('缺少完整终点后机具观察窗口')
    final = [r['data'] for r in samples if r['sim'] >= samples[-1]['sim'] - 1.]
    if len(final) < 3:
        raise ValueError('最后1秒实际机具状态不足3组')
    if car == 'Car3':
        passed = all(row and max(abs(v) for v in row.values()) < 1. for row in final)
    else:
        passed = all(len(row) == 20 and row[0] == 0 and row[19] == 0 and row[15] == 1 for row in final)
    return dict(passed=passed, samples=len(samples), final_samples=final,
                observation_sim_seconds=samples[-1]['sim'] - samples[0]['sim'],
                scope='Normal completion only; no timeout or fault injection')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--run', required=True)
    parser.add_argument('--car', choices=('Car3', 'Car4'), required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    import rclpy
    from rclpy.node import Node
    from rclpy.qos import qos_profile_sensor_data
    from rosgraph_msgs.msg import Clock
    from sensor_msgs.msg import JointState
    from std_msgs.msg import Float64MultiArray
    rclpy.init()
    node = Node('hil_ui_passive_end_work')
    clock, samples = [None], []
    node.create_subscription(Clock, '/clock',
        lambda m: clock.__setitem__(0, m.clock.sec + m.clock.nanosec * 1e-9), qos_profile_sensor_data)

    def receive(message):
        if clock[0] is not None:
            data = (dict(zip(message.name, message.velocity)) if args.car == 'Car3' else list(message.data))
            samples.append(dict(sim=clock[0], data=data))

    topic = '/hil/work/joint_states' if args.car == 'Car3' else '/hil_hitch/powertrain/plant_state'
    node.create_subscription(JointState if args.car == 'Car3' else Float64MultiArray,
                             topic, receive, qos_profile_sensor_data)
    result = dict(passed=False, run_id=args.run, hardware_commands=False)
    try:
        deadline = time.monotonic() + 90
        while time.monotonic() < deadline:
            state = json.loads((args.root / '.local/hil/state.json').read_text())
            if state.get('run_id') != args.run or state.get('phase') != 'completed':
                raise ValueError('机具观察窗口尚未结束就停止或切换了工况')
            rclpy.spin_once(node, timeout_sec=.05)
            if samples and samples[-1]['sim'] - samples[0]['sim'] >= 5.:
                result.update(evaluate(args.car, samples))
                break
        else:
            raise TimeoutError('终点后仿真时钟或机具状态未继续更新')
    except Exception as error:
        result['error'] = str(error)
    finally:
        result['raw_samples'] = samples
        with args.output.open('x') as stream:
            stream.write(json.dumps(result, indent=2) + '\n')
        node.destroy_node()
        rclpy.try_shutdown()
    if not result['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
