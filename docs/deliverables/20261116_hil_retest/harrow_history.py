"""Passively cross-check persistent ground segments against actual implement state."""
import argparse
import json
from pathlib import Path
import signal
import subprocess
import threading
import time


def analyze(rows):
    states = sorted((r for r in rows if r['kind'] == 'plant'), key=lambda r: r['sim_time'])
    histories = sorted((r for r in rows if r['kind'] == 'history'), key=lambda r: r['sim_time'])
    violations, growth, held = [], [], []
    for old, new in zip(histories, histories[1:]):
        a, b = old['poses'], new['poses']
        if len(b) % 2 or len(b) < len(a) or b[:len(a)] != a:
            violations.append(dict(sim_time=new['sim_time'], reason='Existing segment history changed or disappeared'))
        window = [s for s in states if old['sim_time'] <= s['sim_time'] <= new['sim_time']]
        covered = len(window) >= 2 and max(y['sim_time']-x['sim_time'] for x, y in zip(window, window[1:])) <= .15
        if len(b) > len(a):
            growth.append(dict(sim_time=new['sim_time'], added_segments=(len(b)-len(a))//2))
            if not covered or not any(s['values'][2] == 1 for s in window):
                violations.append(dict(sim_time=new['sim_time'], reason='Growth without covered actual engaged state'))
        if a and covered and all(s['values'][2] == 0 and s['values'][3] > .08 for s in window):
            held.append(dict(start=old['sim_time'], end=new['sim_time'], segments=len(a)//2))
            if b != a:
                violations.append(dict(sim_time=new['sim_time'], reason='Raised nonworking implement added ground segments'))
    held_seconds = sum(s['end']-s['start'] for s in held)
    return dict(passed=bool(growth and held_seconds >= 1. and not violations),
        scope='Read-only actual Gazebo segment history and simulation-stamped implement state; not visual/soil-physics certification',
        plant_samples=len(states), history_samples=len(histories),
        maximum_segments=max((len(r['poses'])//2 for r in histories), default=0),
        growth_intervals=growth, raised_history_preserved_sim_seconds=held_seconds,
        raised_intervals=held, violations=violations)


def record(args):
    from google.protobuf import descriptor_pb2, descriptor_pool, message_factory, text_format
    import rclpy
    from rclpy.node import Node
    from rclpy.qos import qos_profile_sensor_data
    from std_msgs.msg import Float64MultiArray
    args.output.mkdir(parents=True, exist_ok=False)
    proto = '/usr/include/gazebo-11/gazebo/msgs/proto'
    descriptors = args.output/'gazebo-poses.pb'
    subprocess.run(['protoc', '-I', proto, '--include_imports',
        '--descriptor_set_out='+str(descriptors), 'poses_stamped.proto'], check=True, timeout=10.)
    pool = descriptor_pool.DescriptorPool()
    for descriptor in descriptor_pb2.FileDescriptorSet.FromString(descriptors.read_bytes()).file:
        pool.Add(descriptor)
    message = message_factory.MessageFactory(pool).GetPrototype(pool.FindMessageTypeByName('gazebo.msgs.PosesStamped'))
    topics = subprocess.check_output(['gz', 'topic', '-l'], text=True, timeout=10.).splitlines()
    topics = [t for t in topics if t.endswith('/hil_hitch/worked_swaths')]
    if len(topics) != 1:
        raise RuntimeError('Exactly one isolated worked-swath topic is required')
    rows, errors, lock = [], [], threading.Lock()
    stream = (args.output/'observations.jsonl').open('x')
    def save(row):
        with lock:
            stream.write(json.dumps(row)+'\n')
            rows.append(row)
    child = subprocess.Popen(['gz', 'topic', '-u', '-e', topics[0]],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, bufsize=1)
    def listen():
        try:
            for line in child.stdout:
                if not line.strip():
                    continue
                value = text_format.Parse(line, message())
                save(dict(kind='history', sim_time=value.time.sec+value.time.nsec*1e-9,
                    poses=[[p.position.x, p.position.y, p.position.z,
                            p.orientation.x, p.orientation.y, p.orientation.z, p.orientation.w] for p in value.pose]))
        except BaseException as error:
            errors.append(str(error))
    thread = threading.Thread(target=listen, daemon=True)
    thread.start()
    rclpy.init()
    node = Node('hil_harrow_history_readonly')
    node.create_subscription(Float64MultiArray, '/hil_hitch/plant_state',
        lambda m: save(dict(kind='plant', sim_time=m.data[8], values=list(m.data))) if len(m.data) == 9 else None,
        qos_profile_sensor_data)
    result = dict(passed=False)
    try:
        deadline = time.monotonic()+1800.
        while time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=.1)
            state = json.loads(args.state.read_text())
            if state.get('run_id') != args.run or state.get('phase') in ('completed', 'failed', 'stopped'):
                result['terminal_phase'] = state.get('phase')
                break
            if errors:
                raise RuntimeError('History observer failed: '+str(errors))
        else:
            raise TimeoutError('Read-only history observer deadline reached')
    except KeyboardInterrupt:
        result['interrupted'] = True
    finally:
        child.send_signal(signal.SIGINT)
        try:
            child.wait(timeout=5.)
        except subprocess.TimeoutExpired:
            child.terminate()
            child.wait(timeout=5.)
        thread.join(timeout=3.)
        stream.close()
        result.update(analyze(rows))
        if errors:
            result.update(passed=False, observer_errors=errors)
        (args.output/'result.json').write_text(json.dumps(result, indent=2)+'\n')
        node.destroy_node()
        rclpy.try_shutdown()
    print(json.dumps(result, indent=2))
    if not result['passed']:
        raise SystemExit(1)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--state', type=Path, required=True)
    parser.add_argument('--run', required=True)
    args = parser.parse_args()
    record(args)


if __name__ == '__main__':
    main()
