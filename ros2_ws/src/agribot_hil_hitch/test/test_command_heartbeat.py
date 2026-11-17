"""High-level HIL settings must renew wall-clock leases even with paused /clock."""
import json
import os
from pathlib import Path
import subprocess
import sys


def test_command_heartbeats_continue_with_frozen_simulation_clock(tmp_path):
    scripts = Path(__file__).resolve().parents[1]/'scripts'
    code = """
import json, sys, time
from pathlib import Path
sys.path.insert(0, sys.argv[1])
import rclpy
from std_msgs.msg import String
from mission import Mission
rclpy.init(args=[], domain_id=171)
node = Mission(Path(sys.argv[2]), transmission='cvt_auto')
counts = {'hitch': 0, 'power': 0}
def receive(name):
    counts[name] += 1
subscriptions = [node.create_subscription(String, topic, lambda msg, name=name: receive(name), 10)
    for name, topic in [('hitch', '/hil_hitch/command'), ('power', '/hil_hitch/powertrain/command')]]
try:
    end = time.monotonic()+1.
    while time.monotonic() < end:
        rclpy.spin_once(node, timeout_sec=.01)
    print(json.dumps(dict(counts=counts, clock_ns=node.get_clock().now().nanoseconds)))
finally:
    node.record.close()
    node.destroy_node()
    rclpy.shutdown()
"""
    env = dict(os.environ, ROS_DOMAIN_ID='171', ROS_LOCALHOST_ONLY='1', RMW_IMPLEMENTATION='rmw_fastrtps_cpp')
    env.pop('CYCLONEDDS_URI', None)
    result = subprocess.run([sys.executable, '-c', code, str(scripts), str(tmp_path)],
                            env=env, capture_output=True, text=True, timeout=15.)
    assert result.returncode == 0, result.stderr
    observation = json.loads(result.stdout.splitlines()[-1])
    assert observation['clock_ns'] == 0
    assert observation['counts']['hitch'] >= 5
    assert observation['counts']['power'] >= 5
