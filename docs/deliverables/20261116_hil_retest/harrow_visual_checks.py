"""Bounded physical hitch/drive work segment with passive history and GUI evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import signal
import subprocess
import sys
import time

import rclpy
from geometry_msgs.msg import Twist
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray, String
from rclpy.qos import qos_profile_sensor_data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--transmission', choices=('powershift_auto', 'cvt_auto'), required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    scripts = root/'docs/deliverables/20261116_hil_retest'
    sys.path.insert(0, str(root/'ros2_ws/src/agribot_hil_hitch/scripts'))
    from verify_powertrain import Bench
    from powertrain_wire import MODES
    args.physical = True
    args.output.mkdir(parents=True, exist_ok=False)
    state_file = root/'.local/hil/state.json'
    run = json.loads(state_file.read_text())['run_id']

    class WorkBench(Bench):
        def __init__(self):
            self.target = dict(mode='hold', position=0, speed=0)
            self.depths, self.roller = [], 0.
            super().__init__(args)
            self.create_subscription(Float64MultiArray, '/hil_hitch/disc_bottom_z',
                lambda m: setattr(self, 'depths', list(m.data)), qos_profile_sensor_data)
            self.create_subscription(JointState, '/hil_hitch/joint_states', self.joints, 10)

        def joints(self, message):
            self.roller = dict(zip(message.name, message.velocity)).get('hitch_cage_roller_joint', 0.)

        def send(self):
            self.commands.write(json.dumps(dict(at=time.monotonic(), speed=self.speed, target=self.target,
                                                transmission=MODES[self.mode]))+'\n')
            velocity = Twist()
            velocity.linear.x = float(self.speed)
            self.velocity_pub.publish(velocity)
            self.hitch_pub.publish(String(data=json.dumps(self.target)))
            self.profile_pub.publish(String(data=json.dumps(dict(mode=MODES[self.mode], gear=0,
                engine_rpm=1600, ratio=0, shift_ms=600, ratio_rate=150))))

    rclpy.init()
    node = WorkBench()
    children, handles = [], []
    def child(name, command):
        handle = (args.output/(name+'-console.log')).open('x')
        handles.append(handle)
        process = subprocess.Popen(['/usr/bin/python3', *command], stdout=handle, stderr=subprocess.STDOUT)
        children.append(process)
        return process
    def picture(condition):
        return child('visual-'+condition, [str(scripts/'harrow_picture.py'), '--output',
            str(args.output/('visual-'+condition)), '--condition', condition,
            '--state', str(state_file), '--run', run])
    try:
        mapped = []
        for proc in Path('/proc').glob('[0-9]*'):
            try:
                if b'gzclient' not in (proc/'cmdline').read_bytes() or \
                        'agribot-app-hil-pc.service' not in (proc/'cgroup').read_text():
                    continue
                for line in (proc/'maps').read_text().splitlines():
                    fields = line.split(maxsplit=5)
                    if len(fields) == 6 and fields[-1].endswith('/libagribot_hil_hitch_tillage.so'):
                        path = Path(fields[-1])
                        if int(fields[4]) != path.stat().st_ino:
                            raise RuntimeError('GUI loaded an outdated tillage library inode')
                        mapped.append(dict(pid=int(proc.name), path=str(path),
                            sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
            except (FileNotFoundError, PermissionError, ProcessLookupError):
                continue
        mapped = list({(v['pid'], v['path']): v for v in mapped}.values())
        node.check('actual_gui_tillage_library_loaded', bool(mapped), mapped)
        node.report['loaded_visual_libraries'] = mapped
        history = child('history', [str(scripts/'harrow_history.py'), '--output', str(args.output/'history'),
            '--state', str(state_file), '--run', run])
        node.configure_auth(False)
        node.check('physical_feedback_ready', node.pump(20., lambda: node.ready and node.hitch is not None))
        node.configure_auth(True)
        node.mode = MODES.index(args.transmission)
        node.target = dict(mode='position', position=0, speed=80)
        node.check('actual_hitch_lowered', node.pump(35., lambda:
            abs(node.hitch[0]) < 150 and abs(node.hitch[1]) < 150))
        node.speed = .25
        node.check('actual_forward_work', node.pump(45., lambda: node.hitch[2] == 1 and node.hitch[4] > .05))
        node.check('all_sixteen_discs_and_original_roller_engaged', node.pump(5., lambda:
            len(node.depths) == 16 and all(-.15 < z < -.025 for z in node.depths) and abs(node.roller) > .1),
            dict(disc_bottom_z=node.depths, actual_roller_rad_s=node.roller))
        working = picture('work')
        node.check('working_pictures_captured', node.pump(15., lambda: working.poll() is not None)
                   and working.returncode == 0)
        began = node.get_clock().now().nanoseconds*1e-9
        node.check('persistent_work_interval', node.pump(30., lambda:
            node.get_clock().now().nanoseconds*1e-9-began >= 12.))
        raised = picture('raised')
        node.pump(.5)
        node.target = dict(mode='position', position=9000, speed=80)
        node.check('actual_hitch_raised_during_forward_travel', node.pump(35., lambda:
            abs(node.hitch[0]-9000) < 150 and abs(node.hitch[1]) < 150 and node.hitch[2] == 0))
        node.check('raised_pictures_captured', node.pump(15., lambda: raised.poll() is not None)
                   and raised.returncode == 0)
        began = node.get_clock().now().nanoseconds*1e-9
        node.check('raised_nonwork_interval', node.pump(20., lambda:
            node.get_clock().now().nanoseconds*1e-9-began >= 5.))
        node.speed, node.mode = 0., 0
        node.check('actual_chassis_stopped', node.pump(10., lambda: abs(node.state[10]) < .02))
        history.send_signal(signal.SIGINT)
        node.check('history_grows_only_at_work_and_is_preserved',
                   node.pump(20., lambda: history.poll() is not None) and history.returncode == 0)
        node.report.update(passed=True, visual_review='pending',
            note='Dedicated bounded controller segment, not a default-route navigation acceptance')
    except BaseException as error:
        node.report['error'] = str(error)
        raise
    finally:
        node.speed, node.mode, node.target = 0., 0, dict(mode='hold', position=0, speed=0)
        try:
            node.pump(.5)
            node.configure_auth(False)
        finally:
            for process in children:
                if process.poll() is None:
                    process.send_signal(signal.SIGINT)
                    try:
                        process.wait(timeout=10.)
                    except subprocess.TimeoutExpired:
                        process.terminate()
                        process.wait(timeout=5.)
            for handle in handles:
                handle.close()
            node.save()
            node.samples.close()
            node.commands.close()
            node.destroy_node()
            rclpy.try_shutdown()


if __name__ == '__main__':
    main()
