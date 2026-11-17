"""Sequential, fail-closed physical retest jobs on the already authorized HIL bench."""
import argparse
import json
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--jobs', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--execute', action='store_true')
    args = parser.parse_args()
    if not args.execute:
        raise SystemExit('No hardware actions without --execute')
    root = args.root.resolve()
    scripts = root/'docs/deliverables/20261116_hil_retest'
    jobs = json.loads(args.jobs.read_text())
    profiles = {'ackermann_apple', 'tracked_sprayer', 'tracked_shredder',
                'tractor_powershift', 'tractor_cvt'}
    if not isinstance(jobs, list) or not jobs:
        raise ValueError('Explicit, nonempty reviewed jobs are required')
    for job in jobs:
        if job['profile'] not in profiles or job['kind'] not in ('mission', 'controller'):
            raise ValueError('Unsupported job')
        if not job['name'].replace('-', '').isalnum():
            raise ValueError('Invalid evidence identity')
    args.output.mkdir(parents=True, exist_ok=False)
    report = dict(scope='Sequential real bench jobs; case verdicts require separate full-criteria review',
                  passed=False, jobs=[])

    def save():
        (args.output/'result.json').write_text(json.dumps(report, indent=2)+'\n')

    def run(command, name):
        with (args.output/(name+'.log')).open('x') as stream:
            code = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT,
                                  timeout=3000).returncode
        report['jobs'][-1]['commands'].append(dict(command=command, returncode=code))
        save()
        if code:
            raise RuntimeError('Retest job failed; original results retained: '+name)

    try:
        for job in jobs:
            directory = args.output/job['name']
            entry = dict(**job, passed=False, commands=[])
            report['jobs'].append(entry)
            save()
            command = ['/usr/bin/python3', str(scripts/(job['kind']+'_checks.py')),
                       '--root', str(root), '--output', str(directory),
                       '--profile', job['profile'], '--execute']
            if job['kind'] == 'controller':
                command += ['--checks', *job['checks']]
            run(command, job['name'])
            if job['kind'] == 'mission':
                run(['/usr/bin/python3', str(scripts/'postcheck.py'), '--root', str(root),
                     '--directory', str(directory)], job['name']+'-postcheck')
            else:
                state = json.loads((directory/'controller-checks.json').read_text())
                run(['/usr/bin/python3', str(scripts/'collect_run.py'), '--root', str(root),
                     '--run', state['run_id'], '--output', str(directory/'hardware-evidence')],
                    job['name']+'-archive')
            entry['passed'] = True
            save()
        report['passed'] = True
    except BaseException as error:
        report['error'] = str(error)
        # Child harnesses normally clean up themselves. Also request an ordinary
        # disarm on a timeout; never kill an in-progress firmware programmer.
        sys.path.insert(0, str(root/'tools/hil'))
        from qualify import stop_and_wait
        try:
            stop_and_wait()
        except Exception as failure:
            report['cleanup_error'] = str(failure)
        raise
    finally:
        save()
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
