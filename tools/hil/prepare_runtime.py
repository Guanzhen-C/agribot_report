"""Build only the app-owned HIL overlay, on the PC or the X5."""
import argparse
import json
import os
from pathlib import Path
import subprocess

from bundle import materialize

STACK = '02f3fe910de5fdc73e2014394c2e80900490b9cc'
ATTACHER = 'b056289ba93ccb549db98926dcbb9679642d0c8d'


def run(command, cwd=None, env=None):
    print('+ '+str(command), flush=True)
    subprocess.run(list(map(str, command)), cwd=cwd, env=env, check=True)


def dependency(directory, url, revision, patch=None):
    if not directory.exists():
        run(['git', 'clone', '--filter=blob:none', '--no-checkout', url, directory])
        run(['git', '-C', directory, 'checkout', '--detach', revision])
    actual = subprocess.check_output(['git', '-C', directory, 'rev-parse', 'HEAD'], text=True).strip()
    if actual != revision:
        raise ValueError('Dependency revision differs: '+str(directory))
    if patch:
        applied = subprocess.run(['git', '-C', directory, 'apply', '--reverse', '--check', patch],
                                 stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0
        if not applied:
            run(['git', '-C', directory, 'apply', '--check', patch])
            run(['git', '-C', directory, 'apply', patch])


def prepare(root, code, role, underlay):
    root, code, underlay = Path(root), Path(code).resolve(), Path(underlay).resolve()
    if root.is_symlink():
        raise ValueError('HIL destination cannot be a symlink')
    root = root.resolve()
    expected = Path.home()/'agribot_app_hil_ws' if role == 'rdk' else code/'.local/hil'
    if root != expected:
        raise ValueError('Refusing a destination outside the dedicated app HIL workspace')
    if not underlay.is_file():
        raise FileNotFoundError(underlay)
    root.mkdir(parents=True, exist_ok=True)
    source = code/'ros2_ws/src'
    bundle = code/'resources/hil/scenarios'
    revision = (bundle/'bundle.json').read_bytes()
    if (root/'runtime').exists():
        if not (root/'bundle.json').is_file() or (root/'bundle.json').read_bytes() != revision:
            raise RuntimeError('Runtime bundle changed; stop HIL and explicitly remove only its generated runtime directory')
        materialize(bundle, root/'runtime', refresh=True)
    else:
        materialize(bundle, root/'runtime')
        (root/'bundle.json').write_bytes(revision)
    (root/'agent.json').write_text(json.dumps({'source_root': str(code), 'underlay': str(underlay)}, indent=2)+'\n')
    catalog = code/'unity/Assets/AgribotConfig/Resources/hil_profiles.json'
    (root/'catalog.json').write_bytes(catalog.read_bytes())
    stack = root/'external/AgIsoStack-plus-plus'
    attacher = root/'external/IFRA_LinkAttacher'
    dependency(stack, 'https://github.com/Open-Agriculture/AgIsoStack-plus-plus.git', STACK)
    dependency(attacher, 'https://github.com/IFRA-Cranfield/IFRA_LinkAttacher.git', ATTACHER,
               source/'agribot_hil_apple/patches/ifra_multi_fixture.patch')
    packages = [source/'agribot_hil_apple', attacher/'linkattacher_msgs']
    selected = ['agribot_hil_apple', 'linkattacher_msgs']
    if role == 'pc':
        packages += [source/'agribot_hil_hitch', attacher/'ros2_LinkAttacher']
        selected += ['agribot_hil_hitch', 'ros2_linkattacher']
    else:
        # Upstream ROOT_DIR is compiled into the binary, so reusing the field
        # executable would still write Log/mat_*.txt inside the real workspace.
        packages += [source/'FAST-LIVO2']
        selected += ['fast_livo']
        for name in ('result', 'PCD', 'ref_cur_combine', 'Colmap/sparse/0', 'Colmap/images'):
            (source/'FAST-LIVO2/Log'/name).mkdir(parents=True, exist_ok=True)
    environment = dict(os.environ, PATH='/usr/bin:/bin', MAKEFLAGS='-j2')
    for key in ('AMENT_PREFIX_PATH', 'COLCON_PREFIX_PATH', 'CMAKE_PREFIX_PATH',
                'LD_LIBRARY_PATH', 'PYTHONPATH', 'ROS_PACKAGE_PATH'):
        environment.pop(key, None)
    def ros(command):
        run(['/bin/bash', '-c', 'set -e; source /opt/ros/humble/setup.bash; source "$1"; shift; exec "$@"',
             'hil-build', underlay, *command], cwd=root, env=environment)
    ros(['colcon', '--log-base', root/'log', 'build', '--base-paths', *packages,
         '--packages-select', *selected, '--executor', 'sequential', '--symlink-install',
         '--build-base', root/'build', '--install-base', root/'install',
         '--cmake-args', '-DCMAKE_BUILD_TYPE=RelWithDebInfo', '-DPYTHON_EXECUTABLE=/usr/bin/python3'])
    if role == 'pc':
        ros(['cmake', '-S', source/'agribot_hil_apple/plugins', '-B', root/'build-plugins',
             '-DCMAKE_BUILD_TYPE=RelWithDebInfo', '-DPYTHON_EXECUTABLE=/usr/bin/python3',
             '-DCMAKE_LIBRARY_OUTPUT_DIRECTORY='+str(root/'plugins')])
        ros(['cmake', '--build', root/'build-plugins', '--parallel', '2'])
    else:
        for name, path in (('isobus', 'agribot_hil_apple/isobus'), ('protocol', 'agribot_hil_hitch/protocol')):
            ros(['cmake', '-S', source/path, '-B', root/('build-'+name),
                 '-DCMAKE_BUILD_TYPE=Release', '-DBUILD_ISOBUS=ON', '-DAGISOSTACK_ROOT='+str(stack)])
            ros(['cmake', '--build', root/('build-'+name), '--parallel', '2'])
        run(['ctest', '--test-dir', root/'build-protocol', '--output-on-failure'])
    print(json.dumps({'prepared': True, 'role': role, 'root': str(root)}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', required=True, type=Path)
    parser.add_argument('--code', required=True, type=Path)
    parser.add_argument('--role', required=True, choices=('pc', 'rdk'))
    parser.add_argument('--underlay', required=True, type=Path)
    args = parser.parse_args()
    prepare(args.root, args.code, args.role, args.underlay)
