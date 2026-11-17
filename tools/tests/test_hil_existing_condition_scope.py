from copy import deepcopy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys

import pytest

ROOT = Path(__file__).resolve().parents[2]
HERE = ROOT/'docs/deliverables/20261116_hil_existing_conditions'
spec = importlib.util.spec_from_file_location('existing_hil_scope', HERE/'revise_scope.py')
scope = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scope)


def cases(data):
    return {section['test_prefix']+'-'+case['letter']: case
        for section in data['sections'] for case in section['cases']}


def test_only_the_user_approved_remaining_eleven_change():
    original = json.loads((ROOT/'docs/deliverables/20261112_hil_srs/requirements.json').read_text())
    before = deepcopy(original)
    revised, changes = scope.revised(original)
    assert original == before
    a, b = cases(original), cases(revised)
    assert a.keys() == b.keys() and len(a) == 30
    changed = {key for key in a if a[key] != b[key]}
    assert changed == {'TC-HIL-'+key.split('-')[0]+'-001-'+key.split('-')[1] for key in scope.REVISIONS}
    assert len(changed) == len(changes) == 11
    assert len([key for key in a if a[key] == b[key]]) == 19
    for section in revised['sections']:
        assert len(section['cases']) == 10
        assert [c['letter'] for c in section['cases']] == list('abcdefghij')


def test_revised_scope_records_exclusions_and_keeps_safety_rules():
    original = json.loads((ROOT/'docs/deliverables/20261112_hil_srs/requirements.json').read_text())
    revised, changes = scope.revised(original)
    assert revised['version'] == 'V2.1' and original['version'] == 'V2.0'
    assert all(c['reason'] and c['revised']['excluded_scope'] for c in changes)
    assert revised['environment'] == original['environment']
    assert revised['measurement_rules'] == original['measurement_rules']
    assert '0.30秒' in cases(revised)['TC-HIL-VCU-001-i']['acceptance']


def test_approved_new_job_list_has_no_obstacle_or_spray_barrier_check():
    jobs = json.loads((ROOT/'docs/deliverables/20261116_hil_retest/existing_conditions_jobs.json').read_text())
    assert jobs and all(job['checks'] == ['harrow'] for job in jobs)
    for name in ['review_records.py', 'revise_scope.py']:
        source = (HERE/name).read_text()
        assert 'SpawnEntity' not in source
        assert 'spawn_entity' not in source


@pytest.mark.parametrize('check', ['visual', 'navigation'])
def test_retired_insertion_checks_are_refused_before_hardware_or_output(tmp_path, check):
    output = tmp_path/'never-created'
    result = subprocess.run([sys.executable, str(ROOT/'docs/deliverables/20261116_hil_retest/controller_checks.py'),
        '--root', str(tmp_path), '--output', str(output), '--checks', check, '--execute'],
        capture_output=True, text=True, timeout=5.)
    assert result.returncode != 0
    assert '禁止新增场景物体' in result.stderr
    assert not output.exists()


def test_versioned_scope_matches_the_generated_revision():
    original = json.loads((ROOT/'docs/deliverables/20261112_hil_srs/requirements.json').read_text())
    revised, _ = scope.revised(original)
    assert json.loads((HERE/'scope_v21/requirements.json').read_text()) == revised
