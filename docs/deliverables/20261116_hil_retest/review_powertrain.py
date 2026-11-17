"""Cross-check the X5 audit against independent PC states and receive gaps."""
import argparse
import hashlib
import json
from pathlib import Path


def review(source_audit, mission_samples, independent, client_events):
    normal = [s for s in mission_samples if s.get('commanded_transmission') != 'off'
              and s.get('powertrain') is not None]
    if not normal:
        return dict(passed=False, error='No normal commanded-transmission window')
    sim_first = min(s['powertrain'][17] for s in normal)
    sim_last = max(s['powertrain'][17] for s in normal)
    wall_first, wall_last = normal[0]['at'], normal[-1]['at']
    # Plant state[17] is Gazebo time, shared by the independent PC recorder and
    # X5 samples. Never compare PC and X5 wall clocks to classify the window.
    pc = [s for s in independent.get('samples', {}).get('power', [])
          if len(s[2]) == 20 and sim_first <= s[2][17] <= sim_last]
    off = [dict(sim_time=s[2][17], speed_mps=s[2][10], enabled=s[2][19], brake=s[2][15])
           for s in pc if s[2][0] == 0 and abs(s[2][10]) > .03]
    status_at = sorted(s['at'] for s in client_events
                       if s.get('event', {}).get('kind') == 'powertrain_status'
                       and wall_first <= s['at'] <= wall_last)
    gaps = [dict(wall_seconds=b-a, from_x5_monotonic=a, to_x5_monotonic=b)
            for a, b in zip(status_at, status_at[1:])]
    excessive = [g for g in gaps if g['wall_seconds'] > .30]
    covered = bool(len(pc) >= 3 and pc[0][2][17] <= sim_first+.5 and pc[-1][2][17] >= sim_last-.5)
    return dict(scope='Normal-route cross-check, not whole 30-case acceptance',
        passed=bool(source_audit.get('audit_passed') and covered and len(status_at) >= 3
                    and not off and not excessive),
        original_x5_audit_passed=source_audit.get('audit_passed'),
        x5_unplanned_off_samples=source_audit.get('unplanned_off_samples'),
        normal_sim_interval=[sim_first, sim_last], normal_x5_wall_interval=[wall_first, wall_last],
        independent_pc_samples=len(pc), independent_window_covered=covered,
        pc_unplanned_off_while_moving_samples=off,
        pc_unplanned_off_while_moving_count=len(off),
        existing_status_timeout_wall_seconds=.30, x5_status_records=len(status_at),
        maximum_x5_status_callback_gap_wall_seconds=max((g['wall_seconds'] for g in gaps), default=None),
        status_gaps_exceeding_existing_timeout=excessive,
        measurement_note='PC samples outside the normal simulated-time window, including commanded final disarm, are excluded; missing X5 samples cannot erase PC observations.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run-directory', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    root = args.run_directory
    paths = dict(source=root/'powertrain-audit.json',
                 mission=root/'hardware-evidence/x5/mission/samples.jsonl',
                 independent=root/'independent/independent.json',
                 events=root/'hardware-evidence/x5/client.jsonl')
    source = json.loads(paths['source'].read_text())
    independent = json.loads(paths['independent'].read_text())
    records = lambda p: [json.loads(s) for s in p.read_text().splitlines()]
    result = review(source, records(paths['mission']), independent, records(paths['events']))
    result['sha256'] = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths.values()}
    args.output.write_text(json.dumps(result, indent=2)+'\n')
    print(json.dumps({k: result[k] for k in ('passed', 'original_x5_audit_passed',
        'x5_unplanned_off_samples', 'pc_unplanned_off_while_moving_count',
        'maximum_x5_status_callback_gap_wall_seconds')}, indent=2))
    if not result['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
