"""Read the running isolated X5 controller's loaded MPPI libraries and hashes."""
import argparse
import hashlib
import json
from pathlib import Path
import shlex
import subprocess
import sys


REMOTE = r'''
import hashlib,json,pathlib
root=pathlib.Path('/home/sunrise/agribot_app_hil_ws')
current=json.loads((root/'current_rdk.json').read_text())
modules=[]
names=['mission.py','tracked_orchard_mission.py','async_trace.py']
if current['profile'].startswith('tracked_'):
    names.append('isobus_work_client.py')
for name in names:
    source=root/'ros2_ws/src/agribot_hil_apple/agribot_hil_apple'/name
    installed=root/'install/agribot_hil_apple/lib/python3.10/site-packages/agribot_hil_apple'/name
    if not installed.exists():
        installed=root/'build/agribot_hil_apple/agribot_hil_apple'/name
    modules.append(dict(name=name,source=str(source),installed=str(installed),
        source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
        installed_sha256=hashlib.sha256(installed.read_bytes()).hexdigest()))
controllers=[]
for path in pathlib.Path('/proc').glob('[0-9]*/cmdline'):
    try:
        argv=path.read_bytes().split(b'\0')
        group=(path.parent/'cgroup').read_text()
        if b'/opt/ros/humble/lib/nav2_controller/controller_server' not in argv:
            continue
        if not group.rstrip().endswith('/agribot-app-hil-rdk.service'):
            continue
        lines=(path.parent/'maps').read_text().splitlines()
        libraries=sorted({line.split()[-1] for line in lines if 'libmppi_' in line})
        controllers.append(dict(pid=int(path.parent.name),argv=[v.decode() for v in argv if v],
            cgroup=group.strip(),libraries=[dict(path=p,sha256=hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest())
                                          for p in libraries]))
    except (OSError,UnicodeError):
        continue
print(json.dumps(dict(current=current,controllers=controllers,python_modules=modules)))
'''


def validate(value, run):
    if value['current']['run_id'] != run:
        raise RuntimeError('Isolated X5 run changed during library attestation')
    controllers = value['controllers']
    if len(controllers) != 1:
        raise RuntimeError('Exactly one isolated native controller must be running')
    libraries = controllers[0]['libraries']
    expected = '/home/sunrise/agribot_app_hil_ws/install/nav2_mppi_controller/lib/'
    if not libraries or any(not row['path'].startswith(expected) for row in libraries):
        raise RuntimeError('Controller is not using exclusively the independent HIL MPPI build')
    if {Path(row['path']).name for row in libraries} != {'libmppi_controller.so', 'libmppi_critics.so'}:
        raise RuntimeError('Both native MPPI controller and critics libraries must be loaded')
    modules = value['python_modules']
    required = {'mission.py', 'tracked_orchard_mission.py', 'async_trace.py'}
    if value['current'].get('profile', '').startswith('tracked_'):
        required.add('isobus_work_client.py')
    if {row['name'] for row in modules} != required:
        raise RuntimeError('Installed Python module attestations are incomplete')
    if any(row['source_sha256'] != row['installed_sha256'] for row in modules):
        raise RuntimeError('Installed HIL Python code differs from the deployed source')


def pc_drive_library(root):
    expected = (root/'.local/hil/plugins/libagribot_hil_diff_drive.so').resolve()
    mapped = []
    for path in Path('/proc').glob('[0-9]*/cmdline'):
        try:
            if b'gzserver' not in path.read_bytes() or \
                    'agribot-app-hil-pc.service' not in (path.parent/'cgroup').read_text():
                continue
            for line in (path.parent/'maps').read_text().splitlines():
                fields = line.split(maxsplit=5)
                if len(fields) != 6 or 'diff_drive.so' not in fields[5]:
                    continue
                library = Path(fields[5])
                if library.resolve() != expected or int(fields[4]) != expected.stat().st_ino:
                    raise RuntimeError('Car3 must load the current app-owned acceleration-fixed plugin')
                mapped.append(dict(pid=int(path.parent.name), path=str(library),
                                   sha256=hashlib.sha256(library.read_bytes()).hexdigest()))
        except (FileNotFoundError, ProcessLookupError, PermissionError):
            continue
    mapped = list({(row['pid'],row['path']):row for row in mapped}.values())
    if len(mapped) != 1:
        raise RuntimeError('Exactly one live Car3 HIL drive-plugin mapping is required')
    return mapped[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--run', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    root = args.root.resolve()
    sys.path.insert(0, str(root/'tools/hil'))
    from config import load
    config = load(validate_flash=False)
    command = ['ssh', '-i', config['identity'], '-o', 'IdentitiesOnly=yes', '-o',
               'UserKnownHostsFile='+config['known_hosts'], '-o', 'StrictHostKeyChecking=yes',
               '-o', 'ConnectTimeout=8', '-o', 'BatchMode=yes', config['host'],
               'python3 -c '+shlex.quote(REMOTE)]
    report = dict(passed=False, hardware_outputs=False,
                  scope='Actual isolated X5 /proc maps, not source-only build evidence')
    try:
        result = subprocess.run(command, check=True, capture_output=True, text=True, timeout=45)
        report.update(json.loads(result.stdout))
        validate(report, args.run)
        if report['current']['profile'].startswith('tracked_'):
            report['pc_drive_library'] = pc_drive_library(root)
        patch = root/'ros2_ws/src/agribot_hil_apple/patches/nav2_humble_mppi_failure.patch'
        report['patch_sha256'] = hashlib.sha256(patch.read_bytes()).hexdigest()
        report['passed'] = True
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        args.output.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
