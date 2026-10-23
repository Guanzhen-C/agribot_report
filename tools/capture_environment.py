#!/usr/bin/env python3
"""Record tool/library versions, never usernames, machine IDs or credentials."""
import json
import platform
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def command(argv):
    result = subprocess.run(argv, text=True, capture_output=True, check=False)
    return {'returncode': result.returncode, 'output': result.stdout.strip()}


def main():
    packages = ['libgtsam-dev', 'libgtsam-unstable-dev', 'ros-humble-gtsam',
                'libpcl-dev', 'libopencv-dev', 'ros-humble-sophus', 'gazebo',
                'ros-humble-nav2-mppi-controller', 'ros-humble-nav2-smac-planner',
                'ros-humble-spatio-temporal-voxel-layer']
    data = {'os': platform.freedesktop_os_release().get('PRETTY_NAME'),
            'architecture': platform.machine(), 'system_python': platform.python_version(),
            'cmake': command(['/usr/bin/cmake', '--version']),
            'node': command(['node', '--version']), 'npm': command(['npm', '--version']),
            'packages': command(['dpkg-query', '-W', '-f=${Package}\t${Version}\n', *packages]),
            'unity_project_version': (ROOT / 'unity/ProjectSettings/ProjectVersion.txt').read_text().strip(),
            'scope': 'Versions on the test host, not a clean-OS installation certification.'}
    (ROOT / 'manifests/tested_environment.json').write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    main()
