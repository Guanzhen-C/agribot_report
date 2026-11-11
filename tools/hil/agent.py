"""Own only app HIL units in the dedicated workspace; never stop real services."""
import argparse
import json
import os
from pathlib import Path
import re
import shlex
import subprocess
import sys


def execute(command, check=True):
    result = subprocess.run(list(map(str, command)), stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            text=True, timeout=60)
    if check and result.returncode:
        raise RuntimeError(result.stdout[-3000:])
    return result


class Agent:
    def __init__(self, root, role):
        self.root, self.role = Path(root).resolve(), role
        if role == 'rdk' and self.root != Path.home()/'agribot_app_hil_ws':
            raise ValueError('Remote HIL root must be the separate agribot_app_hil_ws')
        self.settings = json.loads((self.root/'agent.json').read_text())
        self.code = Path(self.settings['source_root'])
        self.catalog = json.loads((self.root/'catalog.json').read_text())

    def select(self, identifier):
        item = next((p for p in self.catalog['profiles'] if p['id'] == identifier), None)
        if item is None:
            raise ValueError('Unknown HIL profile')
        return item

    def unit(self, mission=False):
        return 'agribot-app-hil-'+('mission' if mission else self.role)+'.service'

    def environment(self, profile):
        env = dict(os.environ)
        scene = self.root/'runtime'/profile['runtime']
        env.update(ROS_DOMAIN_ID=str(profile['domain']), ROS_LOCALHOST_ONLY='0',
            RMW_IMPLEMENTATION='rmw_cyclonedds_cpp',
            CYCLONEDDS_URI='file://'+str(self.code/'ros2_ws/src/agribot_hil_apple/config'/('dds_'+self.role+'.xml')),
            GAZEBO_MASTER_URI='http://127.0.0.1:'+str(profile['gazeboPort']),
            GAZEBO_MODEL_DATABASE_URI='', GAZEBO_IP='127.0.0.1',
            AGRIBOT_HIL_SCENE_ROOT=str(scene), HITCH_SCENE_ROOT=str(scene),
            AGRIBOT_HIL_WORKSPACE=str(self.root),
            AGRIBOT_HIL_CODE_ROOT=str(self.code),
            AGRIBOT_HIL_PLUGIN_ROOT=str(self.root/'plugins'),
            AGRIBOT_ISOBUS_ENDPOINT=str(self.root/'build-isobus/hil_isobus_endpoint'))
        if self.role == 'pc':
            env['GAZEBO_MODEL_PATH'] = ':'.join((str(self.code/'ros2_ws/src/agribot_ackermann_mppi/models'),
                str(self.code/'ros2_ws/src'), env.get('GAZEBO_MODEL_PATH', '')))
        paths = [str(self.root/'install/agribot_hil_hitch/lib'), str(self.root/'install/lib'),
                 str(self.root/'plugins'), env.get('GAZEBO_PLUGIN_PATH', '')]
        env['GAZEBO_PLUGIN_PATH'] = ':'.join(paths)
        return env

    def ros_command(self, profile, command):
        setup = self.settings['underlay']
        overlay = str(self.root/'install/local_setup.bash')
        # Setup paths and all executable arguments are positional, not shell source.
        shell = ('set -e; unset AMENT_PREFIX_PATH COLCON_PREFIX_PATH CMAKE_PREFIX_PATH '
                 'LD_LIBRARY_PATH PYTHONPATH ROS_PACKAGE_PATH; '
                 'source /opt/ros/humble/setup.bash; source "$1"; source "$2"; shift 2; exec "$@"')
        return ['/bin/bash', '-c', shell, 'hil-only', setup, overlay, *map(str, command)]

    def preflight(self, profile):
        selected = self.select(profile)
        scene = self.root/'runtime'/selected['runtime']
        for item in (Path(self.settings['underlay']), self.root/'install/setup.bash', scene/'rdk/mission.json'):
            if not item.is_file():
                raise FileNotFoundError('HIL preparation is incomplete: '+str(item))
        if self.role == 'pc' and not (scene/'world.sdf').is_file():
            raise FileNotFoundError(scene/'world.sdf')
        # A real chassis driver outside an app HIL cgroup is never terminated here.
        if self.role == 'rdk':
            if selected['car'] in ('Car0', 'Car3'):
                executable = self.root/'install/fast_livo/lib/fast_livo/fastlivo_mapping'
                if not executable.is_file() or not executable.resolve().is_relative_to(self.root):
                    raise RuntimeError('Independent HIL FAST-LIVO2 build is missing; field binary reuse is forbidden')
            for line in execute(['ps', '-eo', 'pid=,args=']).stdout.splitlines():
                if any(name in line for name in ('ackermann_chassis_can_node', 'differential_chassis_can_node')):
                    pid = line.strip().split(None, 1)[0]
                    group = (Path('/proc')/pid/'cgroup').read_text()
                    if 'agribot-app-hil-' not in group:
                        raise RuntimeError('A chassis driver outside the app HIL session is running; real services were not changed')
        return {'ready': True, 'role': self.role, 'profile': profile}

    def start(self, profile, run_id, mission=False):
        selected = self.select(profile)
        if not re.fullmatch('[0-9A-Za-z_-]{8,80}', run_id):
            raise ValueError('Invalid HIL run identity')
        directory = self.root/'runs'/run_id
        directory.mkdir(parents=True, exist_ok=True)
        kind = 'mission' if mission else 'runtime'
        command = ['systemd-run', '--user', '--collect', '--property=Type=exec',
            '--unit='+self.unit(mission), '--property=StandardOutput=append:'+str(directory/(self.role+'-'+kind+'.log')),
            '--property=StandardError=inherit', '/usr/bin/python3', self.code/'tools/hil/agent.py',
            '--root', self.root, '--role', self.role, 'exec_role', '--profile', profile,
            '--run-id', run_id, '--kind', kind]
        if self.role == 'pc':
            command[2:2] = ['--setenv=DISPLAY='+os.environ.get('DISPLAY', ':0'),
                '--setenv=XAUTHORITY='+os.environ.get('XAUTHORITY', '/run/user/1000/gdm/Xauthority')]
        execute(command)
        (self.root/('current_'+self.role+'.json')).write_text(json.dumps({'profile': profile, 'run_id': run_id})+'\n')
        return {'started': True, 'unit': self.unit(mission)}

    def command(self, selected, run_id, kind):
        scene = self.root/'runtime'/selected['runtime']
        output = self.root/'runs'/run_id
        apple = self.code/'ros2_ws/src/agribot_hil_apple'
        hitch = self.code/'ros2_ws/src/agribot_hil_hitch'
        if kind == 'mission':
            if self.role != 'rdk':
                raise ValueError('Missions must run on X5')
            if selected['car'] == 'Car4':
                return ['/usr/bin/python3', hitch/'scripts/mission.py', '--manifest', scene/'rdk/mission.json',
                    '--transmission', selected['transmission'], '--start', '--output', output/'mission']
            if selected['car'] == 'Car0':
                return ['ros2', 'run', 'agribot_hil_apple', 'mission', '--scenario', scene/'rdk',
                    '--execute', '--minimum-fruit', '2', '--maximum-fruit', '2', '--output', output/'mission']
            return ['ros2', 'run', 'agribot_hil_apple', 'tracked_orchard_mission', '--mission',
                scene/'rdk/mission.json', '--output', output/'mission.json']
        if selected['car'] == 'Car4':
            args = ['can_trace:='+str(output/(self.role+'-can.jsonl'))]
            if self.role == 'rdk':
                args += ['client_trace:='+str(output/'client.jsonl'), 'powertrain_enabled:=true']
            return ['ros2', 'launch', hitch/'launch'/(self.role+'.launch.py'), *args]
        if selected['car'] == 'Car0':
            return ['ros2', 'launch', apple/'launch'/(self.role+'.launch.py')]
        args = ['scene_root:='+str(scene/'rdk' if self.role == 'rdk' else scene)]
        if self.role == 'rdk':
            args += ['trace:='+str(output/'usb-can.jsonl')]
        return ['ros2', 'launch', apple/'launch'/('tracked_orchard_'+self.role+'.launch.py'), *args]

    def exec_role(self, profile, run_id, kind):
        selected = self.select(profile)
        command = self.ros_command(selected, self.command(selected, run_id, kind))
        os.execve(command[0], list(map(str, command)), self.environment(selected))

    def authorization(self, profile, enabled=False):
        selected = self.select(profile)
        command = ['/usr/bin/python3', self.code/'tools/hil/ros_control.py', 'authorize' if enabled else 'disarm',
                   '--car', selected['car'], '--role', self.role]
        result = subprocess.run(self.ros_command(selected, command), env=self.environment(selected),
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=25)
        if result.returncode:
            raise RuntimeError(result.stdout[-2000:])
        return {'authorized': enabled}

    def stop(self):
        current = self.root/('current_'+self.role+'.json')
        error = None
        try:
            if current.is_file():
                value = json.loads(current.read_text())
                # Stop mission callbacks before the final explicit disarm.
                execute(['systemctl', '--user', 'stop', self.unit(True)], check=False)
                self.authorization(value['profile'])
        except Exception as failure:
            error = failure
        finally:
            execute(['systemctl', '--user', 'stop', self.unit()], check=False)
        if error:
            raise RuntimeError('HIL units stopped, but disarm feedback was not confirmed: '+str(error))
        if current.exists(): current.unlink()
        return {'stopped': True, 'role': self.role}

    def mission_status(self, profile, run_id):
        selected = self.select(profile)
        directory = self.root/'runs'/run_id
        result_path = directory/('mission/result.json' if selected['car'] == 'Car4' else
                                 'mission/mission.json' if selected['car'] == 'Car0' else 'mission.json')
        if result_path.exists():
            value = json.loads(result_path.read_text())
            if value.get('passed') is True or value.get('status') in ('succeeded', 'completed'):
                return {'status': 'completed', 'result': value}
        state = execute(['systemctl', '--user', 'is-active', self.unit(True)], check=False).stdout.strip()
        if state in ('active', 'activating', 'deactivating'):
            return {'status': 'running'}
        return {'status': 'failed', 'message': 'X5 mission ended without a successful result',
                'result_file': str(result_path), 'unit_state': state}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--role', choices=('pc', 'rdk'), required=True)
    parser.add_argument('action', choices=('preflight', 'start', 'stop', 'prepare_bus', 'exec_role',
        'wait_ready', 'mission', 'mission_status', 'disarm', 'authorize'))
    parser.add_argument('--profile')
    parser.add_argument('--run-id')
    parser.add_argument('--kind', choices=('runtime', 'mission'), default='runtime')
    args = parser.parse_args()
    agent = Agent(args.root, args.role)
    if args.action == 'exec_role':
        agent.exec_role(args.profile, args.run_id, args.kind)
    elif args.action == 'prepare_bus':
        execute(['sudo', '-n', '/usr/bin/python3', '-I', agent.code/'tools/hil/admin_bus.py', 'rdk'])
        value = {'prepared': True}
    elif args.action == 'wait_ready':
        selected = agent.select(args.profile)
        command = ['/usr/bin/python3', agent.code/'tools/hil/ros_control.py', 'ready', '--car', selected['car'],
                   '--stop-file', agent.root/'stop.request']
        result = subprocess.run(agent.ros_command(selected, command), env=agent.environment(selected),
            capture_output=True, text=True, timeout=140)
        if result.returncode:
            raise RuntimeError(result.stderr[-2000:]+result.stdout[-2000:])
        value = {'ready': True}
    elif args.action in ('start', 'mission'):
        value = agent.start(args.profile, args.run_id, mission=args.action == 'mission')
    elif args.action == 'mission_status':
        value = agent.mission_status(args.profile, args.run_id)
    elif args.action == 'preflight':
        value = agent.preflight(args.profile)
    elif args.action in ('disarm', 'authorize'):
        value = agent.authorization(args.profile, enabled=args.action == 'authorize')
    else:
        value = agent.stop()
    print(json.dumps(value))


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
