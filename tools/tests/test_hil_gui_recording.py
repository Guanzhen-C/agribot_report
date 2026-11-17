"""Offline regression checks only; no SSH, CAN, flash, ROS launch or bench start."""
import hashlib
import json
from pathlib import Path
from types import SimpleNamespace

import pytest

from tools.hil_recording import audit, service
from tools.hil_recording.end_work import evaluate


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value))


@pytest.fixture
def isolated(tmp_path, monkeypatch):
    monkeypatch.setattr(service, 'ROOT', tmp_path)
    monkeypatch.setattr(service, 'STORE', tmp_path / '.local/hil/recordings')
    monkeypatch.setattr(audit, 'STORE', service.STORE)
    monkeypatch.setattr(service, 'desktop_link', lambda: None)
    calls = []
    monkeypatch.setattr(service.subprocess, 'run', lambda command, **kwargs:
                        calls.append(command) or SimpleNamespace(returncode=0, stderr=''))
    return tmp_path, calls


def request(root, run='20261117T080000_fixture', profile='tracked_sprayer'):
    write(root / '.local/hil/state.json', dict(run_id=run, profile=profile, phase='queued'))
    return service.attach(run, profile)


def test_attach_is_passive_fast_and_idempotent(isolated):
    root, calls = isolated
    first = request(root)
    assert first['phase'] == 'queued'
    assert first['hardware_commands_from_recorder'] is False
    assert request(root)['campaign'] == first['campaign']
    assert len(calls) == 1
    command = calls[0]
    assert command[0] == 'systemd-run'
    assert command[-3:] == ['worker', '--run', '20261117T080000_fixture']
    assert not any(x in command for x in ('start', 'stop', 'flash', 'ros2', 'ssh', 'sudo'))


@pytest.mark.parametrize('run', ['../escape', 'bad-name', 'x;rm', '', 'a'*81])
def test_invalid_run_names_never_spawn_process(isolated, run):
    root, calls = isolated
    with pytest.raises(ValueError):
        request(root, run=run)
    assert not calls


def test_wrong_actual_profile_refused(isolated):
    root, calls = isolated
    write(root / '.local/hil/state.json', dict(run_id='valid_run', profile='tracked_sprayer'))
    with pytest.raises(ValueError):
        service.attach('valid_run', 'tracked_shredder')
    assert not calls


def test_five_starts_share_campaign_repeats_keep_all_attempts(isolated):
    root, calls = isolated
    campaigns = []
    for index, identifier in enumerate(service.PROFILES):
        campaigns.append(request(root, f'run_{index}', identifier)['campaign'])
    assert len(set(campaigns)) == 1
    repeated = request(root, 'another_attempt', 'tracked_sprayer')
    assert repeated['campaign'] == campaigns[0]
    index = service.read(service.STORE / 'current.json')
    assert index['profiles']['tracked_sprayer'] == ['run_1', 'another_attempt']
    index['closed'] = True
    service.save(service.STORE / 'current.json', index)
    assert request(root, 'new_round', 'tracked_sprayer')['campaign'] != campaigns[0]


def test_queue_failure_preserves_visible_failed_status(isolated, monkeypatch):
    root, _ = isolated
    monkeypatch.setattr(service.subprocess, 'run', lambda *args, **kwargs:
                        SimpleNamespace(returncode=1, stderr='unit rejected'))
    with pytest.raises(RuntimeError, match='unit rejected'):
        request(root)
    path = service.run_path('20261117T080000_fixture')
    assert service.read(path / 'status.json')['phase'] == 'failed'
    assert service.read(path / 'request.json')['source'] == 'Unity existing HIL start button'


def test_new_catalog_has_thirty_supported_checks_and_no_injection():
    data = service.read(service.HERE / 'requirements.json')
    assert data['version'] == 'V2.2'
    assert [r['id'] for r in data['cases']] == [f'{group}-{letter}' for group in ('VCU','IMP','NAV') for letter in 'abcdefghij']
    assert len(data['profiles']) == 5
    for row in data['cases']:
        assert hasattr(audit.Evidence, 'check_' + row['check'])
        assert row['acceptance']
        assert row['profiles'] == 'all' or set(row['profiles']).issubset(service.PROFILES)


def test_empty_campaign_cannot_pass_or_reuse_archived_history(tmp_path):
    report = audit.evaluate_campaign(dict(campaign='empty', profiles={}), store=tmp_path)
    assert not report['passed']
    assert report['counts'] == {'通过':0, '不通过':0, '记录中':0, '未运行':30}
    assert len(report['records']) == 30


def test_missing_record_status_is_an_explicit_failure_not_a_pass(tmp_path):
    report = audit.evaluate_campaign(dict(campaign='round', profiles={'tracked_sprayer':['missing_run']}), store=tmp_path)
    assert not report['passed'] and report['counts']['不通过'] > 0
    assert '不可读' in next(r for r in report['records'] if r['id'] == 'TC-HIL-IMP-001-e')['results'][0]['observed']


def test_recording_is_not_a_pass_and_latest_failed_attempt_is_not_hidden(tmp_path):
    for run, phase in [('old_pass','finished'), ('new_fail','failed')]:
        write(tmp_path / 'runs' / run / 'status.json', dict(phase=phase))
    report = audit.evaluate_campaign(dict(campaign='round', profiles={'tracked_sprayer':['old_pass','new_fail']}), store=tmp_path)
    assert report['selected_runs']['tracked_sprayer'] == 'new_fail'
    assert not report['passed'] and report['counts']['不通过'] > 0
    write(tmp_path / 'runs/new_fail/status.json', dict(phase='recording'))
    report = audit.evaluate_campaign(dict(campaign='round', profiles={'tracked_sprayer':['old_pass','new_fail']}), store=tmp_path)
    assert report['counts']['通过'] == 0 and report['counts']['记录中'] > 0


def sealed_fixture(folder):
    spec = (service.HERE / 'requirements.json').read_bytes()
    write(folder / 'request.json', dict(run_id='run_1', profile='tracked_sprayer', requirements_version='V2.2',
                                      requirements_sha256=hashlib.sha256(spec).hexdigest()))
    write(folder / 'hardware-evidence/raw.json', dict(actual=True))
    raw = folder / 'hardware-evidence/raw.json'
    write(folder / 'hardware-evidence/manifest.json', dict(run_id='run_1', files={
          'raw.json':dict(sha256=hashlib.sha256(raw.read_bytes()).hexdigest())}))
    files = {str(p.relative_to(folder)):hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.rglob('*') if p.is_file()}
    write(folder / 'evidence.json', dict(run_id='run_1', files=files))


def test_changed_evidence_fails_closed(tmp_path):
    sealed_fixture(tmp_path)
    audit.Evidence(tmp_path, 'tracked_sprayer', 'run_1').verify()
    write(tmp_path / 'hardware-evidence/raw.json', dict(actual=False))
    with pytest.raises(ValueError, match='发生变化'):
        audit.Evidence(tmp_path, 'tracked_sprayer', 'run_1').verify()


def test_unsealed_late_results_cannot_count_as_this_run(tmp_path):
    sealed_fixture(tmp_path)
    evidence = audit.Evidence(tmp_path, 'tracked_sprayer', 'run_1')
    evidence.verify()
    write(tmp_path / 'late-result.json', dict(passed=True))
    with pytest.raises(ValueError, match='未封存'):
        evidence.json('late-result.json')


@pytest.mark.parametrize('path', ['../escape', '/etc/passwd'])
def test_evidence_manifest_cannot_read_outside_recording(tmp_path, path):
    with pytest.raises(ValueError, match='越界'):
        audit.Evidence(tmp_path, 'tracked_sprayer', 'run_1').raw(path)


@pytest.mark.parametrize('car', ['Car3', 'Car4'])
def test_normal_end_work_requires_full_actual_tail(car):
    data = {'fan':0., 'pump':0.} if car == 'Car3' else [0.]*20
    if car == 'Car4':
        data[15] = 1.
    rows = [dict(sim=i/10, data=data) for i in range(51)]
    assert evaluate(car, rows)['passed']
    with pytest.raises(ValueError):
        evaluate(car, rows[:10])
    wrong = dict(data) if car == 'Car3' else list(data)
    wrong['pump' if car == 'Car3' else 19] = 20
    rows[-1] = dict(sim=5., data=wrong)
    assert not evaluate(car, rows)['passed']


def test_terminal_tail_needs_three_final_samples():
    with pytest.raises(ValueError, match='不足3组'):
        evaluate('Car3', [dict(sim=x, data={'fan':0}) for x in (0,2,5)])


def test_default_route_reads_native_feedback_schema(tmp_path):
    task = dict(status='succeeded', navigation=[dict(requested_goals=[[1,0,0],[2,0,0],[3,0,0]])],
        manifest=dict(fixed_route_points=[[1,0,0],[2,0,0],[3,0,0]], goal_tolerance_m=.4),
        independent_endpoint_error_m=.1,
        route_passage_events=[dict(source='native_nav2_remaining_path_feedback',from_index=0,to_index=1),
                              dict(source='native_nav2_remaining_path_feedback',from_index=1,to_index=2)])
    write(tmp_path / 'hardware-evidence/pc/mission-result.json', dict(status='completed', result=task))
    assert audit.Evidence(tmp_path,'tracked_sprayer','run_1').check('route')['passed']
    task['route_passage_events'].reverse()
    write(tmp_path / 'hardware-evidence/pc/mission-result.json', dict(status='completed', result=task))
    with pytest.raises(ValueError, match='不连续'):
        audit.Evidence(tmp_path,'tracked_sprayer','run_1').check('route')


def test_worker_attaches_collects_and_never_controls_the_task(isolated, monkeypatch):
    import sys
    root, _ = isolated
    queued = request(root)
    run, identifier = queued['run_id'], queued['profile']
    target = service.run_path(run)
    selected = dict(id=identifier, car='Car3', runtime=identifier)
    class Agent:
        def __init__(self, *args): pass
        def ros_command(self, selected, command): return command
        def environment(self, selected): return {}
    monkeypatch.setitem(sys.modules, 'agent', SimpleNamespace(Agent=Agent))
    monkeypatch.setitem(sys.modules, 'catalog', SimpleNamespace(profile=lambda p:selected))
    monkeypatch.setitem(sys.modules, 'qualify', SimpleNamespace(inspect_run=lambda *args:dict(passed=True)))
    states = iter([dict(run_id=run,phase='running'), dict(run_id=run,phase='completed'),
                   dict(run_id=run,phase='stopped',session_alive=False)])
    monkeypatch.setattr(service, 'current_state', lambda:next(states))
    monkeypatch.setattr(service.time, 'sleep', lambda *args:None)
    monkeypatch.setattr(service, 'snapshot_runtime', lambda *args:
                        write(target/'configuration/rdk/mission.json', dict(finish=[0,0,0])))
    refreshed, commands = [], []
    monkeypatch.setattr(service, 'refresh_campaign', lambda value:refreshed.append(value))
    class Child:
        pid = 987654321
        returncode = 0
        def poll(self): return 0
        def wait(self, timeout): return 0
    def spawn(command, **kwargs):
        commands.append(command)
        if any(str(p).endswith('observe_run.py') for p in command):
            write(target/'independent/recorder-manifest.json', dict(recorder_returncode=0))
        return Child()
    monkeypatch.setattr(service.subprocess, 'Popen', spawn)
    service.worker(run)
    status = service.read(target/'status.json')
    capture = service.read(target/'capture.json')
    assert status['phase'] == 'finished'
    assert capture['completed'] and capture['closed'] and not capture['errors']
    assert capture['commands_sent'] is False
    assert refreshed == [queued['campaign']]
    assert service.read(target/'evidence.json')['files']
    assert not any(Path(str(p)).name in ('cli.py','workflow.py','ros_control.py','controller_checks.py','qualify.py')
                   for command in commands for p in command)
