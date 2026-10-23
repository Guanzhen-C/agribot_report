#!/usr/bin/env python3
"""Start only the HTTP gateway on loopback; never start a vehicle task."""
import json
import os
from pathlib import Path
import signal
import subprocess
import tempfile
import time
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def main():
    log_dir = ROOT / '.validation'
    log_dir.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='agribot-console-smoke-') as directory:
        with (log_dir / 'console.log').open('w') as output:
            process = subprocess.Popen([
                'ros2', 'run', 'agribot_mobile_app', 'mobile_gateway', '--ros-args',
                '-p', 'http_host:=127.0.0.1', '-p', 'http_port:=18888',
                '-p', 'map_root:=' + directory, '-p', 'bag_root:=' + directory,
                '-p', 'process_log_root:=' + directory,
                '-p', 'static_root:=' + str(ROOT / 'ros2_ws/src/agribot_mobile_app/web/dist')],
                stdout=output, stderr=subprocess.STDOUT, start_new_session=True,
                env=dict(os.environ, ROS_DOMAIN_ID='94', ROS_LOCALHOST_ONLY='1'))
            try:
                for _ in range(60):
                    if process.poll() is not None:
                        raise RuntimeError('Gateway exited; inspect .validation/console.log')
                    try:
                        with urllib.request.urlopen('http://127.0.0.1:18888/api/v1/state', timeout=1) as response:
                            state = json.load(response)
                        with urllib.request.urlopen('http://127.0.0.1:18888/', timeout=1) as response:
                            assert b'<html' in response.read().lower()
                        print(json.dumps({'http_state': 'ok', 'html': 'ok',
                                          'state_keys': sorted(state)}, ensure_ascii=False))
                        return
                    except OSError:
                        time.sleep(0.5)
                raise RuntimeError('Gateway HTTP startup timeout')
            finally:
                if process.poll() is None:
                    os.killpg(process.pid, signal.SIGINT)
                    try:
                        process.wait(timeout=15)
                    except subprocess.TimeoutExpired:
                        os.killpg(process.pid, signal.SIGTERM)
                        process.wait(timeout=10)


if __name__ == '__main__':
    main()
