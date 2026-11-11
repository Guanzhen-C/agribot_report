"""Physical startup effects for the app-owned PC/X5 HIL session."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time

from catalog import ROOT
from config import ssh_arguments, verify_flash_scripts
from protect_real_runtime import compare, remote_snapshot, RUNTIME_OUTPUTS
from admin_bus import pc_device


class RuntimeBackend:
    def __init__(self, config, run, emit, cancelled=lambda: False):
        self.config, self.run, self.emit, self.cancelled = config, Path(run), emit, cancelled
        self.profile = None
        self.stop_confirmed = False
        self.log = self.run/'operations.log'
        self.run.mkdir(parents=True, exist_ok=True)

    def execute(self, command, timeout=60, env=None, check=True):
        result = subprocess.run([str(item) for item in command], stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT, text=True, timeout=timeout, env=env)
        with self.log.open('a') as stream:
            stream.write(json.dumps({'argv': list(map(str, command)), 'code': result.returncode})+'\n')
            stream.write(result.stdout+'\n')
        if check and result.returncode:
            raise RuntimeError(result.stdout[-3000:] or 'HIL command failed')
        return result

    def agent(self, role, action, *arguments, timeout=60, check=True):
        if role == 'rdk':
            command = ssh_arguments(self.config, 'python3', self.config['remote_root']+'/tools/hil/agent.py',
                '--root', self.config['remote_root'], '--role', role, action, *arguments)
        else:
            command = ['/usr/bin/python3', ROOT/'tools/hil/agent.py', '--root', ROOT/'.local/hil',
                       '--role', role, action, *arguments]
        return self.execute(command, timeout=timeout, check=check)

    def preflight(self, selected, image):
        self.profile = selected
        verify_flash_scripts(self.config)
        if not Path('/usr/local/libexec/agribot-hil-bus').is_file():
            raise RuntimeError('HIL权限助手未安装，请先运行HIL部署准备。')
        # No commands are sent to a debug probe or CAN bus during this check.
        identities = []
        for path in Path('/sys/bus/usb/devices').glob('*'):
            try:
                if (path/'idVendor').read_text().strip() == '1fc9' and (path/'idProduct').read_text().strip() == '5601':
                    identities.append((path/'serial').read_text().strip())
            except (FileNotFoundError, NotADirectoryError):
                continue
        if identities.count(self.config['probe_serial']) != 1:
            raise RuntimeError('没有找到绑定的6750烧录器，未执行烧录。')
        pc_device()
        for path in Path('/sys/class/hidraw').glob('*'):
            for parent in path.resolve().parents:
                try:
                    if (parent/'serial').read_text().strip() == self.config['probe_serial']:
                        if not os.access('/dev/'+path.name, os.R_OK | os.W_OK):
                            raise PermissionError('6750烧录器访问权限未安装，未执行烧录。')
                        break
                except (FileNotFoundError, NotADirectoryError):
                    continue
        self.agent('pc', 'preflight', '--profile', selected['id'])
        self.agent('rdk', 'preflight', '--profile', selected['id'])

    def stop_previous(self):
        # Agent cleanup owns only named HIL units, never the real vehicle service.
        for role in ('rdk', 'pc'):
            self.agent(role, 'stop', timeout=45)
        sync = ROOT/'ros2_ws/src/agribot_vehicle_description/scripts/unity_sync_and_simulate.sh'
        self.execute(['/bin/bash', sync, '--stop'], timeout=60)

    def verify_real_runtime(self):
        baseline = json.loads(Path(self.config['real_runtime_fingerprint']).read_text())
        actual = remote_snapshot(self.config['host'], Path(self.config['known_hosts']), Path(self.config['identity']))
        all_changes = compare(baseline, actual)
        changes = compare(baseline, actual, code_only=True)
        result = {'unchanged': not changes, 'files': len(actual['files']), 'changes': changes,
                  'protected_files': len(actual['files'].keys()-RUNTIME_OUTPUTS),
                  'runtime_output_changes': {name: value for name, value in all_changes.items() if name not in changes}}
        with (self.run/'real_runtime_checks.jsonl').open('a') as stream:
            stream.write(json.dumps(result)+'\n')
        if changes:
            raise RuntimeError('X5真车工作区指纹有变化，已阻止HIL启动，请先核对。')

    def flash(self, image):
        verify_flash_scripts(self.config)
        path = Path(image['path'])
        if any(letter in str(path) for letter in '{};\r\n'):
            raise ValueError('Unsafe firmware path')
        if hashlib.sha256(path.read_bytes()).hexdigest() != image['sha256']:
            raise ValueError('Firmware changed after preflight')
        environment = dict(os.environ)
        library = self.config.get('openocd_library_path', '')
        environment['LD_LIBRARY_PATH'] = library+':'+environment.get('LD_LIBRARY_PATH', '')
        result = self.execute([self.config['openocd'], '-s', self.config['openocd_scripts'],
            '-f', 'probes/cmsis_dap.cfg', '-c', 'adapter serial '+self.config['probe_serial'],
            '-c', 'adapter speed 1000', '-f', 'soc/hpm6750-single-core.cfg',
            '-f', 'boards/hpm6750evk2.cfg', '-c', 'bindto 127.0.0.1', '-c', 'init', '-c', 'halt',
            '-c', 'program {'+str(path)+'} verify', '-c', 'reset run', '-c', 'shutdown'],
            timeout=180, env=environment, check=False)
        (self.run/'flash.log').write_text(result.stdout)
        if result.returncode:
            raise RuntimeError('6750烧录失败，车辆未启动。请检查烧录器连接和访问权限。日志：'+str(self.run/'flash.log'))
        if 'Verified OK' not in result.stdout or 'Programming Finished' not in result.stdout:
            raise RuntimeError('烧录工具未确认编程及校验完成，已阻止运行。')
        (self.run/'firmware.json').write_text(json.dumps(image, indent=2)+'\n')
        # A cancellation never interrupts flash writes; the next step observes it.
        time.sleep(.5)

    def configure_bus(self, bitrate):
        self.execute(['sudo', '-n', '/usr/local/libexec/agribot-hil-bus', 'pc', str(bitrate)])
        self.agent('rdk', 'prepare_bus')

    def start_pc(self, selected):
        self.agent('pc', 'start', '--profile', selected['id'], '--run-id', self.run.name)

    def start_x5(self, selected):
        self.agent('rdk', 'start', '--profile', selected['id'], '--run-id', self.run.name)

    def wait_ready(self, selected):
        self.agent('pc', 'wait_ready', '--profile', selected['id'], timeout=150)

    def start_mission(self, selected):
        # Tractor mission owns its authorization handshake. The legacy chassis
        # bridges start locked and are enabled only after the full readiness gate.
        if selected['car'] != 'Car4':
            self.agent('pc', 'authorize', '--profile', selected['id'])
            self.agent('rdk', 'authorize', '--profile', selected['id'])
        self.agent('rdk', 'mission', '--profile', selected['id'], '--run-id', self.run.name)

    def safe_stop(self):
        if self.stop_confirmed:
            return
        errors = []
        for role in ('rdk', 'pc'):
            try:
                self.agent(role, 'stop', timeout=45)
            except Exception as error:
                errors.append(str(error))
        if errors:
            (self.run/'stop-errors.json').write_text(json.dumps(errors, indent=2)+'\n')
            raise RuntimeError('HIL清理未完全确认：'+'; '.join(errors))
        self.stop_confirmed = True

    def monitor(self, selected):
        while True:
            if self.cancelled():
                self.safe_stop()
                return 'stopped'
            result = self.agent('rdk', 'mission_status', '--run-id', self.run.name,
                                '--profile', selected['id'], timeout=15)
            status = json.loads(result.stdout)
            if status['status'] == 'completed':
                self.agent('rdk', 'disarm', '--profile', selected['id'])
                self.agent('pc', 'disarm', '--profile', selected['id'])
                self.verify_real_runtime()
                (self.run/'mission-result.json').write_text(json.dumps(status, indent=2)+'\n')
                return 'completed'
            if status['status'] == 'failed':
                raise RuntimeError('HIL任务失败：'+status.get('message', '请查看日志'))
            time.sleep(.5)
