"""Physical pump, real Gazebo ray occlusion and screenshots; visual review is separate."""
import argparse
import json
import math
from pathlib import Path
import signal
import subprocess
import threading
import time
import xml.etree.ElementTree as ET

from google.protobuf import descriptor_pb2, descriptor_pool, message_factory, text_format
import rclpy
from gazebo_msgs.srv import SpawnEntity, DeleteEntity
from geometry_msgs.msg import Twist
from rclpy.clock import Clock, ClockType
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import String


def barrier_xml(name):
    sdf = ET.Element('sdf', version='1.6')
    model = ET.SubElement(sdf, 'model', name=name)
    ET.SubElement(model, 'static').text = 'true'
    link = ET.SubElement(model, 'link', name='barrier')
    for kind in ('collision', 'visual'):
        shape = ET.SubElement(link, kind, name='barrier_'+kind)
        box = ET.SubElement(ET.SubElement(shape, 'geometry'), 'box')
        ET.SubElement(box, 'size').text = '.5 .5 .5'
        if kind == 'visual':
            material = ET.SubElement(shape, 'material')
            ET.SubElement(material, 'ambient').text = '.5 .1 .1 1'
            ET.SubElement(material, 'diffuse').text = '.5 .1 .1 1'
    return ET.tostring(sdf, encoding='unicode')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    proto = Path('/usr/include/gazebo-11/gazebo/msgs/proto')
    descriptors = args.output/'gazebo-messages.pb'
    subprocess.run(['protoc', '-I', str(proto), '--descriptor_set_out='+str(descriptors),
                    'gz_string.proto', 'vector3d.proto'], check=True, timeout=10.)
    schema = descriptor_pb2.FileDescriptorSet.FromString(descriptors.read_bytes())
    pool = descriptor_pool.DescriptorPool()
    for item in schema.file:
        pool.Add(item)
    factory = message_factory.MessageFactory(pool)
    types = {kind: factory.GetPrototype(pool.FindMessageTypeByName('gazebo.msgs.'+name))
             for kind, name in [('depth', 'GzString'), ('spray', 'Vector3d')]}
    topics = subprocess.check_output(['gz', 'topic', '-l'], text=True, timeout=10.).splitlines()
    state, errors, lock = {}, [], threading.Lock()
    children, threads, streams = [], [], []

    def observe(kind, process, stream):
        try:
            for line in process.stdout:
                if not line.strip():
                    continue
                msg = text_format.Parse(line, types[kind]())
                data = json.loads(msg.data) if kind == 'depth' else [msg.x, msg.y, msg.z]
                row = dict(kind=kind, received_monotonic=time.monotonic(), value=data)
                stream.write(json.dumps(row)+'\n')
                with lock:
                    state[kind] = row
        except BaseException as error:
            with lock:
                errors.append(str(error))

    resolved_topics = {}
    for kind in types:
        suffix = '/hil/work/'+('spray_depth' if kind == 'depth' else 'spray')
        matching = [topic for topic in topics if topic.endswith(suffix)]
        if len(matching) != 1:
            raise RuntimeError('Exactly one isolated spray topic required: '+suffix)
        resolved_topics[kind] = matching[0]
    for kind in types:
        stream = (args.output/(kind+'.jsonl')).open('x')
        # Small Vector3d echoes otherwise wait for stdio's 4 KiB buffer, while
        # large depth messages flush immediately. Do not relax freshness checks.
        process = subprocess.Popen(['stdbuf', '-oL', '-eL', 'gz', 'topic', '-u', '-e', resolved_topics[kind]],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, bufsize=1)
        thread = threading.Thread(target=observe, args=(kind, process, stream), daemon=True)
        streams.append(stream)
        children.append(process)
        threads.append(thread)
        thread.start()
    rclpy.init()
    node = Node('hil_spray_physical_occlusion_check')
    report = dict(passed=False, checks=[], visual_review='pending',
        scope='Physical CAN pump plus real Gazebo collision rays; screenshot review is required for full IMP-f',
        barrier_name='hil_acceptance_spray_barrier', observations=[])
    work = node.create_publisher(String, '/hil/work/command', 1)
    drive = node.create_publisher(Twist, '/hil/navigation/cmd_vel', 1)
    command = [dict(fan_gear=3, pump_gear=4, shredder_level=0)]
    motors = {}
    node.create_subscription(JointState, '/hil/work/joint_states',
        lambda m: motors.update(zip(m.name, m.velocity)), 20)
    def renew():
        work.publish(String(data=json.dumps(command[0])))
        drive.publish(Twist())
    node.create_timer(.05, renew, clock=Clock(clock_type=ClockType.STEADY_TIME))

    def snapshot(kind):
        with lock:
            if errors:
                raise RuntimeError('Gazebo observer failed: '+str(errors))
            row = state.get(kind)
            if row is None or time.monotonic()-row['received_monotonic'] > 1.:
                return None
            return row['value']

    def pump(seconds, condition=None):
        deadline = time.monotonic()+seconds
        while time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=.02)
            if condition is not None and condition():
                return True
        return False

    def check(name, passed, **details):
        report['checks'].append(dict(name=name, passed=bool(passed), **details))
        if not passed:
            raise AssertionError(name)

    def service(name, kind, request):
        client = node.create_client(kind, name)
        try:
            if not client.wait_for_service(timeout_sec=5.):
                raise RuntimeError(name+' unavailable')
            future = client.call_async(request)
            rclpy.spin_until_future_complete(node, future, timeout_sec=10.)
            if not future.done() or not future.result().success:
                raise RuntimeError(name+' failed')
            return future.result()
        finally:
            node.destroy_client(client)

    def picture(name, position, target):
        # This only changes the GUI camera, never any simulated body pose.
        delta = [b-a for a, b in zip(position, target)]
        yaw = math.atan2(delta[1], delta[0])
        pitch = -math.atan2(delta[2], math.hypot(delta[0], delta[1]))
        quaternion = [-math.sin(pitch/2)*math.sin(yaw/2), math.sin(pitch/2)*math.cos(yaw/2),
                      math.cos(pitch/2)*math.sin(yaw/2), math.cos(pitch/2)*math.cos(yaw/2)]
        message = 'position {x:%s y:%s z:%s} orientation {x:%s y:%s z:%s w:%s}' % (*position, *quaternion)
        camera = next((topic for topic in topics if topic.endswith('/user_camera/joy_pose')), None)
        if camera:
            subprocess.run(['gz', 'topic', '-p', camera, '-m', message], check=True, timeout=5.)
        pump(2.)
        ids = subprocess.check_output(['xdotool', 'search', '--onlyvisible', '--name', '^Gazebo$'], text=True).split()
        if len(ids) != 1:
            raise RuntimeError('Exactly one visible isolated Gazebo window is required')
        subprocess.run(['xdotool', 'windowactivate', '--sync', ids[0]], check=True, timeout=5.)
        subprocess.run(['xwd', '-silent', '-id', ids[0], '-out', str(args.output/(name+'.xwd'))],
                       check=True, timeout=5.)
        report['observations'].append(dict(screenshot=name+'.xwd', gui_camera_position=position,
            camera_topic=camera, actual_motors=dict(motors), actual_spray_input=snapshot('spray')))

    spawned = False
    try:
        check('actual_pump_and_fan_rotating', pump(30., lambda:
            motors.get('work_fan_joint', 0.) > 50 and motors.get('work_pump_joint', 0.) > 50
            and snapshot('depth') is not None and snapshot('spray') is not None
            and snapshot('spray')[0] > 0.))
        depth = snapshot('depth')
        nozzles = depth['nozzles']
        check('original_eight_live_nozzle_rays', len(nozzles) == 8)
        # Select an outward horizontal ray with room beyond the chassis.
        candidates = [(i, n) for i, n in enumerate(nozzles)
                      if abs(n['direction'][2]) < .7 and n['ranges'][len(n['ranges'])//2] > 2.]
        check('open_outward_test_ray_available', bool(candidates))
        index, nozzle = max(candidates, key=lambda item: abs(item[1]['direction'][1]))
        center = [a+b for a, b in zip(nozzle['origin'], nozzle['direction'])]
        request = SpawnEntity.Request(name=report['barrier_name'], xml=barrier_xml(report['barrier_name']),
                                      reference_frame='world')
        request.initial_pose.position.x, request.initial_pose.position.y, request.initial_pose.position.z = center
        request.initial_pose.orientation.w = 1.
        target = nozzle['origin']
        picture('before-obstacle', [target[0]+2.1, target[1]-2.4, target[2]+1.4], target)
        service('/spawn_entity', SpawnEntity, request)
        spawned = True
        def occluded():
            value = snapshot('depth')
            if not value:
                return False
            current = value['nozzles'][index]
            return .3 < current['ranges'][len(current['ranges'])//2] < 1.3
        check('real_collision_truncates_original_nozzle_ray', pump(10., occluded),
              selected_nozzle=index, barrier_center=center)
        report['blocked_depth'] = snapshot('depth')
        picture('with-obstacle', [target[0]+2.1, target[1]-2.4, target[2]+1.4], target)
        picture('opposite-view', [target[0]+1.5, target[1]+2.4, target[2]+1.2], target)
        command[0] = dict(fan_gear=3, pump_gear=0, shredder_level=0)
        check('pump_off_closes_new_particle_emission_exactly', pump(4., lambda:
            snapshot('spray') is not None and snapshot('spray')[0] == 0.),
            observed_spray_input=snapshot('spray'), actual_motors=dict(motors))
        check('actual_pump_stops_but_fan_runs', pump(12., lambda:
            abs(motors.get('work_pump_joint', 100.)) < .5 and motors.get('work_fan_joint', 0.) > 50))
        check('spray_emission_input_stops', pump(4., lambda:
            snapshot('spray') is not None and snapshot('spray')[0] == 0.))
        stopped = snapshot('spray')[2]
        check('existing_particle_lifetime_elapsed', pump(10., lambda:
            snapshot('spray') is not None and snapshot('spray')[2]-stopped >= 3.))
        picture('pump-stopped', [target[0]+2.1, target[1]-2.4, target[2]+1.4], target)
        report['passed'] = True
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        command[0] = dict(fan_gear=0, pump_gear=0, shredder_level=0)
        try:
            pump(1.)
            if spawned:
                service('/delete_entity', DeleteEntity, DeleteEntity.Request(name=report['barrier_name']))
        except BaseException as error:
            report.update(passed=False, cleanup_error=str(error))
            raise
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
    print(json.dumps({k: v for k, v in report.items() if k != 'blocked_depth'}, indent=2))


if __name__ == '__main__':
    main()
