"""Finite read-only Car0 recorder; output stays within the selected new run."""
import argparse
import json
from pathlib import Path
import time

import rclpy
from agribot_hil_apple.mission_record import Recorder


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--run', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    state_file = args.root/'.local/hil/state.json'
    args.output.mkdir(parents=True, exist_ok=True)
    output = args.output/'independent-pc.json'
    if output.exists():
        raise FileExistsError(output)
    deadline = time.monotonic()+1800.
    while time.monotonic() < deadline:
        state = json.loads(state_file.read_text())
        if state.get('run_id') != args.run or state.get('phase') in ('failed', 'stopped'):
            raise RuntimeError('Selected run ended before recording started')
        if state.get('phase') in ('starting_x5', 'waiting_ready', 'starting_mission', 'running'):
            break
        time.sleep(.2)
    else:
        raise TimeoutError('HIL runtime did not start')
    rclpy.init(args=[])
    node = Recorder()
    last, terminal = 0., None
    try:
        while time.monotonic() < deadline:
            rclpy.spin_once(node, timeout_sec=.05)
            now = time.monotonic()
            if now-last < 2.:
                continue
            last = now
            state = json.loads(state_file.read_text())
            traces = node.report['traces'].get('/hil/ground_truth', [])
            can = node.report['can'][-1] if node.report['can'] else {}
            print(json.dumps(dict(phase=state.get('phase'), sim_time=node.clock,
                xy=traces[-1][1:3] if traces else None, errors=can.get('errors'),
                recoveries=can.get('transport_recoveries'), fault=can.get('fault'))), flush=True)
            if state.get('run_id') != args.run:
                break
            if state.get('phase') in ('completed', 'failed', 'stopped'):
                terminal = terminal or now
                if now-terminal >= 3.:
                    break
    finally:
        node.report.update(run_id=args.run, final_sim_time=node.clock,
            wall_duration_sec=time.monotonic()-node.started)
        output.write_text(json.dumps(node.report, indent=2)+'\n')
        node.destroy_node()
        rclpy.try_shutdown()


if __name__ == '__main__':
    main()
