"""Repeat the frozen V2.1 thirty-case HIL procedure; no hardware by default."""
import argparse
import csv
import datetime
import hashlib
import json
from pathlib import Path
import shlex
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SCOPE = ROOT/'docs/deliverables/20261116_hil_existing_conditions'
RETEST = ROOT/'docs/deliverables/20261116_hil_retest'
VISUALS = {
    'spray': ('b-sprayer-controls/spray-existing',
              ['original-nozzles', 'existing-occlusion', 'pump-stopped']),
    'harrow': ('b-cvt-controls/harrow-visual',
               [stage+'/'+view for stage in ('visual-work', 'visual-raised')
                for view in ('rear-right', 'rear-left')]),
}


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024*1024), b''):
            digest.update(block)
    return digest.hexdigest()


def inside(root, name):
    path = (root/name).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError('Path escapes the declared root: '+name)
    return path


def configuration_sha(path, root):
    data = path.read_bytes()
    if path.suffix in ('.json', '.yaml', '.urdf', '.sdf', '.world'):
        data = data.replace(str(root/'.local/hil/runtime').encode(), b'${HIL_RUNTIME}')
        data = data.replace(str(root).encode(), b'${REPO_ROOT}')
    return hashlib.sha256(data).hexdigest()


def verify_reference(reference, root=ROOT):
    baseline = json.loads((reference/'baseline.json').read_text())
    if baseline['schema'] != 1 or not baseline['source_files'] or not baseline['configuration_files']:
        raise ValueError('Incomplete repeat-test baseline')
    for name, expected in baseline['source_files'].items():
        if sha(inside(root, name)) != expected:
            raise ValueError('Frozen code/resource differs: '+name)
    for name, expected in baseline['configuration_files'].items():
        if configuration_sha(inside(root, name), root) != expected:
            raise ValueError('Frozen model/map/parameter differs: '+name)
    history = inside(reference, baseline['historical_recovery']['file'])
    if sha(history) != baseline['historical_recovery']['sha256']:
        raise ValueError('Historical NAV-i evidence changed')
    return baseline, history


def commands(output):
    return [
        ('auxiliary', [sys.executable, str(SCOPE/'run_auxiliary.py'), '--root', str(ROOT),
                       '--output', str(output/'auxiliary'), '--execute']),
        ('physical', [sys.executable, str(RETEST/'campaign.py'), '--root', str(ROOT),
                      '--jobs', str(SCOPE/'phase_b_jobs.json'), '--output', str(output/'physical'), '--execute']),
        ('independent-review', [sys.executable, str(SCOPE/'review_closed.py'), '--root', str(ROOT),
                               '--jobs', str(SCOPE/'phase_b_jobs.json'), '--directory', str(output/'physical')]),
    ]


def run(args):
    if not args.execute or not args.bench_only:
        raise ValueError('Hardware requires both --execute and --bench-only; use plan for a dry run')
    verify_reference(args.reference)
    sys.path.insert(0, str(ROOT/'tools/hil'))
    from cli import read_status
    if read_status().get('phase') != 'stopped':
        raise RuntimeError('Stop the previous HIL normally before starting the suite')
    args.output.mkdir(parents=True, exist_ok=False)
    state = dict(passed=False, stage='starting', started_at=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        reference_baseline_sha256=sha(args.reference/'baseline.json'), steps=[],
        scope='New physical runs; final verdict requires explicit visual review and a separate audit')
    destination = args.output/'execution.json'

    def save():
        destination.write_text(json.dumps(state, indent=2)+'\n')

    save()
    try:
        for name, command in commands(args.output):
            state['stage'] = name
            save()
            with (args.output/(name+'.log')).open('x') as log:
                result = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
            state['steps'].append(dict(name=name, command=command, returncode=result.returncode))
            save()
            if result.returncode:
                raise RuntimeError(name+' failed; preserve this directory and inspect its original log')
        state['stage'] = 'awaiting_visual_review'
    except BaseException as error:
        state.update(stage='failed', error=str(error))
        raise
    finally:
        state['updated_at'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        save()
    print('Physical sessions finished; NOT a thirty-case pass yet. Render and inspect all seven captures before finalize.')


def render(output):
    if shutil.which('ffmpeg') is None:
        raise RuntimeError('ffmpeg is required on the review PC; do not replace captures with sample images')
    for relative, names in VISUALS.values():
        for name in names:
            source = output/'physical'/relative/(name+'.xwd')
            target = source.with_suffix('.png')
            if not source.is_file():
                raise FileNotFoundError(source)
            if not target.exists():
                subprocess.run(['ffmpeg', '-nostdin', '-v', 'error', '-i', str(source),
                    '-frames:v', '1', '-update', '1', str(target)], check=True)
            print(target)


def casebook(requirements, ledger, destination, root=ROOT, evidence_prefix=''):
    sections = json.loads(requirements.read_text())['sections']
    results = {r['id']: r for r in ledger['records']}
    lines = ['30项测试步骤、过程与结果', '范围：V2.1，开发方隔离HIL台架；不是第三方签署。',
        '一组实板任务可覆盖多条观察，不等于30次独立完整行驶。',
        'NAV-i为新软件夹具＋指定历史实板记录核查，不是本轮实板故障注入。',
        '证据路径基准：'+('本保存包根目录。' if evidence_prefix else '仓库根目录；仓库外路径保留绝对地址。'), '']
    for section in sections:
        for case in section['cases']:
            identity = section['test_prefix']+'-'+case['letter']
            result = results[identity]
            lines.extend([identity+' / '+section['name']+' / '+case['title'],
                '适用工况：'+', '.join(case['profiles']), '前置条件：'+case['preconditions'],
                '操作步骤：'+case['steps'], '判定标准：'+case['acceptance'],
                '执行方式：'+result['evidence_mode'], '实际过程与结果：'+result['observed'],
                '结论：'+result['verdict'], '不覆盖范围：'+case.get('excluded_scope', '原判据不变。'), '证据：'])
            for value in result['evidence']:
                path = Path(value).resolve()
                lines.append('  '+(evidence_prefix+str(path.relative_to(root)) if path.is_relative_to(root) else str(path)))
            lines.append('')
    with destination.open('x', encoding='utf-8') as stream:
        stream.write('\n'.join(lines)+'\n')


def finalize(args):
    _, history = verify_reference(args.reference)
    state_path = args.output/'execution.json'
    state = json.loads(state_path.read_text())
    if state['stage'] != 'awaiting_visual_review' or state['reference_baseline_sha256'] != sha(args.reference/'baseline.json'):
        raise ValueError('A closed fresh execution against this baseline is required')
    for relative, _ in VISUALS.values():
        review = args.output/'physical'/relative/'visual-review.json'
        if not review.exists() or json.loads(review.read_text()).get('passed') is not True:
            raise ValueError('Explicit completed visual review is still required: '+str(review))
    ledger = args.output/'cases.json'
    command = [sys.executable, str(SCOPE/'audit_fresh.py'), '--phase', 'b',
        '--directory', str(args.output/'physical'), '--auxiliary', str(args.output/'auxiliary'),
        '--after', state['started_at'], '--historical-recovery', str(history), '--output', str(ledger)]
    with (args.output/'final-audit.log').open('x') as stream:
        result = subprocess.run(command, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT)
    if not ledger.exists():
        raise RuntimeError('Audit did not produce a ledger; inspect final-audit.log')
    value = json.loads(ledger.read_text())
    casebook(SCOPE/'scope_v21/requirements.json', value, args.output/'30项步骤与结果.txt')
    with (args.output/'cases.csv').open('x', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['编号', '类别', '名称', '结论', '执行方式', '实际过程与结果'])
        writer.writerows([r[k] for k in ('id', 'category', 'title', 'verdict', 'evidence_mode', 'observed')]
                         for r in value['records'])
    state.update(stage='finalized', passed=bool(value['passed'] and result.returncode == 0),
        counts=value['counts'], finished_at=datetime.datetime.now(datetime.timezone.utc).isoformat())
    state_path.write_text(json.dumps(state, indent=2)+'\n')
    print(json.dumps(dict(passed=state['passed'], counts=value['counts']), ensure_ascii=False))
    if not state['passed']:
        raise SystemExit(1)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('plan', 'check', 'run', 'render', 'finalize'))
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--bench-only', action='store_true')
    args = parser.parse_args(argv)
    args.output = args.output.resolve()
    if args.action not in ('plan', 'render') and args.reference is None:
        parser.error('--reference is required')
    if args.reference:
        args.reference = args.reference.resolve()
    if args.action == 'plan':
        print('DRY RUN: no SSH, flashing, CAN writes or task start.')
        for name, command in commands(args.output):
            print(name+': '+shlex.join(command))
        print('Then render seven captures, explicitly review them, and finalize. NAV-i remains software + historical review.')
    elif args.action == 'check':
        value, _ = verify_reference(args.reference)
        print(json.dumps(dict(passed=True, hardware_outputs=False, source_files=len(value['source_files']),
            configuration_files=len(value['configuration_files']))))
    elif args.action == 'run':
        run(args)
    elif args.action == 'render':
        render(args.output)
    else:
        finalize(args)


if __name__ == '__main__':
    main()
