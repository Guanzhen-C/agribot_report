"""Build/run the software-only native MPPI contract probe and retain every exit code."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root, output = args.root.resolve(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    sys.path.insert(0, str(root/'tools/hil'))
    from agent import Agent
    agent = Agent(root/'.local/hil', 'pc')
    selected = agent.select('tractor_cvt')
    environment = dict(os.environ, ROS_DOMAIN_ID='225', ROS_LOCALHOST_ONLY='1',
                       RMW_IMPLEMENTATION='rmw_fastrtps_cpp', PATH='/usr/bin:/bin')
    environment.pop('CYCLONEDDS_URI', None)
    source = root/'docs/deliverables/20261116_hil_retest'
    report = dict(passed=False, hardware_outputs=False,
                  scope='Software-only native MPPI blocked/free fixtures; not a physical obstacle test', steps=[])
    def run(name, command, timeout):
        with (output/(name+'.log')).open('x') as stream:
            code = subprocess.run(agent.ros_command(selected, command), env=environment, stdout=stream,
                                  stderr=subprocess.STDOUT, timeout=timeout).returncode
        report['steps'].append(dict(name=name, command=command, returncode=code))
        if code:
            raise RuntimeError(name+' failed; see original log')
    try:
        run('configure', ['/usr/bin/cmake', '-S', str(source), '-B', str(output/'build'),
                          '-DCMAKE_BUILD_TYPE=RelWithDebInfo'], 120.)
        run('build', ['/usr/bin/cmake', '--build', str(output/'build'), '--parallel', '2'], 180.)
        run('probe', [str(output/'build/hil_mppi_contract_probe')], 30.)
        events = [json.loads(line) for line in (output/'probe.log').read_text().splitlines()
                  if line.startswith('{')]
        if not events or events[-1] != dict(passed=True, actual_native_optimizer_typed_failures=2,
                                           same_controller_recovered=True, hardware_outputs=False):
            raise RuntimeError('Native MPPI probe did not confirm the complete contract')
        report['result'] = events[-1]
        files = [source/'mppi_contract_probe.cpp', root/
                 'ros2_ws/src/agribot_hil_apple/patches/nav2_humble_mppi_failure.patch',
                 root/'.local/hil/install/nav2_mppi_controller/lib/libmppi_controller.so']
        report['sha256'] = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
        report['passed'] = True
    except BaseException as error:
        report['error'] = str(error)
        raise
    finally:
        (output/'result.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
