"""Fresh physical spray display check using only pre-existing scene geometry."""
import argparse
import json
import math
from pathlib import Path
import signal
import subprocess
import threading
import time

from google.protobuf import descriptor_pb2, descriptor_pool, message_factory, text_format
import rclpy
from geometry_msgs.msg import Twist
from rclpy.clock import Clock, ClockType
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import String


def blocked_rays(packet):
    hits = []
    for index, nozzle in enumerate(packet['nozzles']):
        size = nozzle['size']
        for ray, distance in enumerate(nozzle['ranges']):
            sx = (2.*(ray % size)/(size-1)-1.)*nozzle['tangent']
            sy = (2.*(ray // size)/(size-1)-1.)*nozzle['tangent']
            direction = [d+sx*u+sy*v for d, u, v in zip(nozzle['direction'], nozzle['u'], nozzle['v'])]
            norm = math.sqrt(sum(x*x for x in direction))
            direction = [x/norm for x in direction]
            hit = [o+distance*d for o, d in zip(nozzle['origin'], direction)]
            if .1 < distance < 4.4 and hit[2] > .08 and abs(direction[2]) < .5:
                hits.append(dict(nozzle=index, ray=ray, distance_m=distance, hit_world=hit,
                                 origin=nozzle['origin'], direction=direction))
    return hits


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    proto = Path('/usr/include/gazebo-11/gazebo/msgs/proto')
    descriptor = args.output/'gazebo-messages.pb'
    subprocess.run(['protoc', '-I', str(proto), '--descriptor_set_out='+str(descriptor),
                    'gz_string.proto', 'vector3d.proto'], check=True, timeout=10.)
    pool = descriptor_pool.DescriptorPool()
    for item in descriptor_pb2.FileDescriptorSet.FromString(descriptor.read_bytes()).file:
        pool.Add(item)
    factory = message_factory.MessageFactory(pool)
    types = {kind: factory.GetPrototype(pool.FindMessageTypeByName('gazebo.msgs.'+name))
             for kind, name in [('depth', 'GzString'), ('spray', 'Vector3d')]}
    topics = subprocess.check_output(['gz', 'topic', '-l'], text=True, timeout=10.).splitlines()
    state, errors, lock = {}, [], threading.Lock()
    children, threads, streams = [], [], []
    report = dict(passed=False, checks={}, added_scene_objects=False, visual_review='pending',
                  scope='Physical CAN pump and original scene collision rays; no added objects or body pose changes',
                  observations=[])

    def observe(kind, child, stream):
        try:
            for line in child.stdout:
                if not line.strip():
                    continue
                msg = text_format.Parse(line, types[kind]())
                value = json.loads(msg.data) if kind == 'depth' else [msg.x, msg.y, msg.z]
                row = dict(received_monotonic=time.monotonic(), value=value)
                stream.write(json.dumps(row)+'\n')
                with lock:
                    state[kind] = row
        except BaseException as error:
            errors.append(str(error))

    rclpy.init()
    node = Node('hil_spray_existing_scene_check')
    work = node.create_publisher(String, '/hil/work/command', 1)
    drive = node.create_publisher(Twist, '/hil/navigation/cmd_vel', 1)
    command, motors = [dict(fan_gear=3, pump_gear=4, shredder_level=0)], {}
    node.create_subscription(JointState, '/hil/work/joint_states', lambda m: motors.update(zip(m.name, m.velocity)), 20)

    def renew():
        work.publish(String(data=json.dumps(command[0])))
        drive.publish(Twist())

    node.create_timer(.05, renew, clock=Clock(clock_type=ClockType.STEADY_TIME))

    def snapshot(kind):
        with lock:
            if errors:
                raise RuntimeError(errors)
            row = state.get(kind)
            return row['value'] if row and time.monotonic()-row['received_monotonic'] < 1. else None

    def pump(seconds, condition=None):
        end = time.monotonic()+seconds
        while time.monotonic() < end:
            rclpy.spin_once(node, timeout_sec=.02)
            if condition and condition():
                return True
        return False

    def check(name, value):
        report['checks'][name] = bool(value)
        if not value:
            raise AssertionError(name)

    def picture(name, position, target):
        delta = [b-a for a, b in zip(position, target)]
        yaw = math.atan2(delta[1], delta[0])
        pitch = -math.atan2(delta[2], math.hypot(delta[0], delta[1]))
        quat = [-math.sin(pitch/2)*math.sin(yaw/2), math.sin(pitch/2)*math.cos(yaw/2),
                math.cos(pitch/2)*math.sin(yaw/2), math.cos(pitch/2)*math.cos(yaw/2)]
        camera, = [t for t in topics if t.endswith('/user_camera/joy_pose')]
        message = 'position {x:%s y:%s z:%s} orientation {x:%s y:%s z:%s w:%s}' % (*position, *quat)
        subprocess.run(['gz', 'topic', '-p', camera, '-m', message], check=True, timeout=5.)
        pump(2.)
        window, = subprocess.check_output(['xdotool', 'search', '--onlyvisible', '--name', '^Gazebo$'], text=True).split()
        subprocess.run(['xdotool', 'windowactivate', '--sync', window], check=True, timeout=5.)
        subprocess.run(['xwd', '-silent', '-id', window, '-out', str(args.output/(name+'.xwd'))], check=True, timeout=5.)
        report['observations'].append(dict(screenshot=name+'.xwd', gui_camera_position=position,
                                          actual_motors=dict(motors), actual_spray_input=snapshot('spray')))

    try:
        for kind in types:
            suffix = '/hil/work/'+('spray_depth' if kind == 'depth' else 'spray')
            topic, = [t for t in topics if t.endswith(suffix)]
            stream = (args.output/(kind+'.jsonl')).open('x')
            child = subprocess.Popen(['stdbuf', '-oL', '-eL', 'gz', 'topic', '-u', '-e', topic],
                stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, bufsize=1)
            thread = threading.Thread(target=observe, args=(kind, child, stream), daemon=True)
            children.append(child)
            streams.append(stream)
            threads.append(thread)
            thread.start()
        check('actual_motors_and_emission', pump(30., lambda: motors.get('work_fan_joint', 0.) > 50
            and motors.get('work_pump_joint', 0.) > 50 and snapshot('depth') is not None
            and snapshot('spray') is not None and snapshot('spray')[0] > 0.))
        packet = snapshot('depth')
        check('eight_original_nozzles_with_depth_grids', len(packet['nozzles']) == 8
            and all(len(n['ranges']) == 81 for n in packet['nozzles']))
        check('existing_scene_blocks_non_ground_rays', pump(15., lambda:
            snapshot('depth') is not None and bool(blocked_rays(snapshot('depth')))))
        report['blocked_depth'] = snapshot('depth')
        report['existing_collision_hits'] = blocked_rays(report['blocked_depth'])
        target = report['existing_collision_hits'][0]['origin']
        picture('original-nozzles', [target[0]+2.1, target[1]-2.4, target[2]+1.4], target)
        picture('existing-occlusion', [target[0]-1.5, target[1]+2.0, target[2]+1.2], target)
        command[0] = dict(fan_gear=3, pump_gear=0, shredder_level=0)
        check('pump_off_stops_new_emission', pump(4., lambda: snapshot('spray') is not None and snapshot('spray')[0] == 0.))
        report['pump_off_actual_motors'] = dict(motors)
        check('actual_pump_stops_fan_keeps_running', pump(12., lambda:
            abs(motors.get('work_pump_joint', 100.)) < .5 and motors.get('work_fan_joint', 0.) > 50))
        began = snapshot('spray')[2]
        check('particle_lifetime_elapsed', pump(10., lambda: snapshot('spray') is not None
            and snapshot('spray')[2]-began >= 3. and snapshot('spray')[0] == 0.))
        picture('pump-stopped', [target[0]+2.1, target[1]-2.4, target[2]+1.4], target)
        report['passed'] = True
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        command[0] = dict(fan_gear=0, pump_gear=0, shredder_level=0)
        try:
            pump(1.)
        finally:
            for child in children:
                child.send_signal(signal.SIGINT)
            for child in children:
                try:
                    child.wait(timeout=5.)
                except subprocess.TimeoutExpired:
                    child.terminate()
                    child.wait(timeout=5.)
            for thread in threads:
                thread.join(timeout=2.)
            for stream in streams:
                stream.close()
            (args.output/'result.json').write_text(json.dumps(report, indent=2)+'\n')
            node.destroy_node()
            rclpy.try_shutdown()
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
