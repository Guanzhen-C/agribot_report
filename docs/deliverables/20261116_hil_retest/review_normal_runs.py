"""Offline, evidence-bound review of the five completed normal physical missions."""
import argparse
import hashlib
import json
from pathlib import Path

RUNS = [('ackermann_apple', 'apple-native-r2', 2),
        ('tracked_sprayer', 'sprayer-native-r1', 1),
        ('tracked_shredder', 'shredder-native-r1', 1),
        ('tractor_powershift', 'normal-campaign-r3/powershift-native-r2', 5),
        ('tractor_cvt', 'normal-campaign-r3/cvt-native-r1', 5)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    report = dict(passed=False, scope='Five completed normal runs only; not all 30 original criteria', runs=[])
    for profile, folder, expected_plans in RUNS:
        directory = args.evidence/folder
        inputs = {}
        def read(name):
            path = directory/name
            inputs[name] = hashlib.sha256(path.read_bytes()).hexdigest()
            return json.loads(path.read_text())
        checks = {name: read(name).get('passed') is True for name in
                  ('mission-checks.json', 'postcheck.json', 'sensor-audit.json', 'terminal.json')}
        attestation = directory/'runtime-attestation.json'
        code_evidence = 'not collected in this earlier run; later controller attestation is separate evidence'
        if attestation.exists():
            checks['runtime_attestation'] = read('runtime-attestation.json')['passed'] is True
            code_evidence = 'actual runtime attestation retained'
        mission, terminal = read('mission-checks.json'), read('terminal.json')
        checks['protected_real_files_unchanged'] = mission['real_files_changed'] == {}
        plans = sum(len(v) for v in terminal['observed_planner_actions'].values())
        checks['normal_stage_plan_count'] = plans == expected_plans
        checks['no_new_post_goal_plan'] = terminal['new_planner_goals'] == []
        pose = read('pose-audit.json')
        checks['stamped_pose_measurements_present'] = bool(pose['localization_xy']['samples'] and pose['path_tracking_xy']['samples'])
        if profile == 'ackermann_apple':
            statuses = read('independent/independent-pc.json')['can']
        else:
            observed = read('independent/independent.json')
            statuses = [v[2] for v in observed['samples']['can_status' if profile.startswith('tractor_') else 'status']]
            checks['no_transport_events'] = observed['transport_events'] == []
        faults = [v for v in statuses if v.get('fault') or v.get('errors', 0) or v.get('transport_recoveries', 0)]
        checks['normal_physical_can_has_no_fault_or_recovery'] = bool(statuses) and not faults
        manifest = read('hardware-evidence/manifest.json')
        mismatched = [name for name, value in manifest['files'].items()
            if hashlib.sha256((directory/'hardware-evidence'/name).read_bytes()).hexdigest() != value['sha256']]
        checks['closed_raw_evidence_hashes_match'] = not mismatched
        if profile.startswith('tractor_'):
            checks['original_powertrain_audit'] = read('powertrain-audit.json')['audit_passed'] is True
            checks['independent_powertrain_audit'] = read('powertrain-independent-audit.json')['passed'] is True
        report['runs'].append(dict(profile=profile, run_id=mission['run_id'], directory=folder,
            passed=all(checks.values()), checks=checks, physical_can_status_samples=len(statuses),
            physical_can_fault_samples=len(faults), normal_planning_actions=plans,
            final_error_m=terminal['final_error_m'], final_maximum_speed_mps=terminal['maximum_planar_speed_mps'],
            localization_xy=pose['localization_xy'], tracking_xy=pose['path_tracking_xy'],
            localization_match_coverage=pose['localization_match_coverage'], input_sha256=inputs))
        report['runs'][-1]['code_attestation_note'] = code_evidence
    report['passed'] = all(r['passed'] for r in report['runs'])
    args.output.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps({k: v for k, v in report.items() if k != 'runs'}, indent=2))
    for run in report['runs']:
        print(run['profile'], run['passed'], {k: v for k, v in run['checks'].items() if not v})
    if not report['passed']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
