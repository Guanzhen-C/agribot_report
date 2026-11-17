"""Preserve the completed run and a read-only version lock, without hardware actions."""
import argparse
import datetime
import json
import os
from pathlib import Path
import shutil
import subprocess

from repeat_suite import ROOT, SCOPE, HERE, casebook, sha, configuration_sha, inside


def source_paths(root):
    folders = ['tools/hil', 'docs/deliverables/20261116_hil_retest',
        'docs/deliverables/20261116_hil_existing_conditions',
        'docs/deliverables/20261117_hil_repeatable',
        'ros2_ws/src/agribot_hil_apple', 'ros2_ws/src/agribot_hil_hitch', 'resources/hil']
    files = {p for folder in folders for p in (root/folder).rglob('*')
             if p.is_file() and not {'__pycache__', '.pytest_cache'} & set(p.parts) and p.suffix != '.pyc'}
    files.update(root/name for name in [
        'unity/Assets/AgribotConfig/Resources/hil_profiles.json',
        'ros2_ws/src/gazebo_ros_pkgs/gazebo_plugins/src/gazebo_ros_diff_drive.cpp',
        'ros2_ws/src/gazebo_ros_pkgs/gazebo_plugins/include/gazebo_plugins/wheel_velocity_limit.hpp'])
    return sorted(files)


def save(args):
    if args.output.is_relative_to(ROOT):
        raise ValueError('Store the preserved reference outside the source repository')
    ledger_path = ROOT/'.validation/hil_acceptance_20261116/fresh-b-cases-r1.json'
    ledger = json.loads(ledger_path.read_text())
    if not ledger['passed'] or ledger['case_count'] != 30:
        raise ValueError('A completed thirty-case reference is required')
    # Check all asserted source evidence before preserving any reference verdict.
    for name, expected in ledger['input_sha256'].items():
        source = Path(name).resolve()
        if not source.is_relative_to(ROOT) or sha(source) != expected:
            raise ValueError('Reference evidence changed or escaped the repository: '+name)
    args.output.mkdir(parents=True, exist_ok=False)
    records = {}
    counts = dict(hardlinks=0, copies=0)

    def preserve(source, relative):
        target = inside(args.output, relative)
        if target.exists():
            return
        target.parent.mkdir(parents=True, exist_ok=True)
        expected = sha(source)
        try:
            if not source.is_relative_to(ROOT/'.validation'):
                raise OSError('Mutable inputs and editable reports require an independent copy')
            os.link(source, target)
            counts['hardlinks'] += 1
        except OSError:
            shutil.copy2(source, target)
            counts['copies'] += 1
        if sha(target) != expected:
            raise ValueError('Archive verification failed: '+relative)
        records[relative] = dict(sha256=expected, bytes=target.stat().st_size)

    sources = {Path(name) for name in ledger['input_sha256']}
    for name in ledger['selected_run_directories'].values():
        folder = Path(name)
        sources.update(p for p in folder.rglob('*') if p.is_file())
    for relative in ['fresh-b-aux-r1', 'fresh-b-aux-r2', 'fresh-b-aux-r3',
                     'fresh-30-r1/b-sprayer-controls', 'fresh-11-r1/a-sprayer-normal',
                     'fresh-11-rest-r1/a-apple-controls']:
        folder = ROOT/'.validation/hil_acceptance_20261116'/relative
        sources.update(p for p in folder.rglob('*') if p.is_file())
    sources.add(ledger_path)
    sources.add(ROOT/'.validation/hil_acceptance_20261116/final-fresh-unit-r2.xml')
    for source in sorted(sources):
        if not source.resolve().is_relative_to(ROOT):
            raise ValueError('Only repository evidence may be archived')
        preserve(source, 'records/'+str(source.relative_to(ROOT)))
    for source in args.documents.iterdir():
        if source.is_file():
            preserve(source, 'reports/'+source.name)
    history = 'records/.validation/hil_acceptance_20261116/sprayer-r1/hardware-evidence/x5/mission.json'
    configurations = {}
    runtime = ROOT/'.local/hil/runtime'
    for folder in ('ackermann_apple', 'tracked_sprayer', 'tracked_shredder', 'tractor_harrow'):
        scene = runtime/folder
        paths = {p for p in (scene/'rdk').rglob('*') if p.is_file() and '__pycache__' not in p.parts}
        paths.update(p for p in scene.iterdir() if p.is_file() and p.suffix in ('.json', '.sdf', '.urdf'))
        for path in sorted(paths):
            configurations[str(path.relative_to(ROOT))] = configuration_sha(path, ROOT)
    baseline = dict(schema=1, created_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        requirements_version='V2.1', source_commit=subprocess.check_output(
            ['git', '-C', str(ROOT), 'rev-parse', 'HEAD'], text=True).strip(),
        source_state='SHA-256 lock of the actual working tree, including uncommitted files; not a clean Git release',
        source_files={str(p.relative_to(ROOT)):sha(p) for p in source_paths(ROOT)},
        configuration_files=configurations,
        normalization='Exact local runtime and repository root prefixes only; binary assets are never normalized',
        historical_recovery=dict(file=history, sha256=sha(args.output/history)),
        reference_loaded_runtime={k: json.loads((Path(v)/'runtime-attestation.json').read_text())
            for k,v in ledger['selected_run_directories'].items()})
    (args.output/'baseline.json').write_text(json.dumps(baseline, ensure_ascii=False, indent=2)+'\n')
    (args.output/'archive-index.json').write_text(json.dumps(dict(files=records, storage=counts,
        note='Closed evidence only; hardlinks avoid duplicate data and survive removal of the original directory. Do not edit either copy.'),
        ensure_ascii=False, indent=2)+'\n')
    casebook(SCOPE/'scope_v21/requirements.json', ledger, args.output/'30项测试步骤过程与结果.txt', evidence_prefix='records/')
    shutil.copy2(HERE/'使用说明.txt', args.output/'使用说明.txt')
    (args.output/'复测入口').symlink_to(HERE, target_is_directory=True)
    print(json.dumps(dict(saved=str(args.output), evidence_files=len(records), storage=counts,
        hardware_outputs=False), ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--documents', type=Path, required=True)
    args = parser.parse_args()
    args.output, args.documents = args.output.resolve(), args.documents.resolve()
    save(args)


if __name__ == '__main__':
    main()
