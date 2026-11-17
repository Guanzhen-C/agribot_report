import ast
import importlib.util
import json
from pathlib import Path
import sys
from types import SimpleNamespace

import pytest

ROOT = Path(__file__).resolve().parents[2]
HERE = ROOT/'docs/deliverables/20261117_hil_repeatable'
spec = importlib.util.spec_from_file_location('repeat_suite_tested', HERE/'repeat_suite.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def test_plan_never_calls_subprocess_or_creates_output(tmp_path, monkeypatch, capsys):
    monkeypatch.setattr(module.subprocess, 'run', lambda *a, **k: pytest.fail('Dry run reached a subprocess'))
    output = tmp_path/'not-created'
    module.main(['plan', '--output', str(output)])
    assert not output.exists()
    assert 'DRY RUN' in capsys.readouterr().out


@pytest.mark.parametrize('execute,bench', [(False, False), (False, True), (True, False)])
def test_hardware_requires_both_explicit_flags(tmp_path, monkeypatch, execute, bench):
    monkeypatch.setattr(module, 'verify_reference', lambda *a: pytest.fail('Gate was bypassed'))
    with pytest.raises(ValueError, match='both'):
        module.run(SimpleNamespace(execute=execute, bench_only=bench, output=tmp_path/'new', reference=tmp_path))


@pytest.mark.parametrize('failure_at', [None, 1])
def test_fresh_execution_is_fail_closed_and_never_auto_approves_visuals(tmp_path, monkeypatch, failure_at):
    reference = tmp_path/'reference'
    reference.mkdir()
    (reference/'baseline.json').write_text('{}')
    output, called = tmp_path/'new-run', []
    monkeypatch.setattr(module, 'verify_reference', lambda *a: ({}, None))
    monkeypatch.setitem(sys.modules, 'cli', SimpleNamespace(read_status=lambda: {'phase':'stopped'}))

    def child(command, **kwargs):
        called.append(command)
        return SimpleNamespace(returncode=1 if len(called)-1 == failure_at else 0)

    monkeypatch.setattr(module.subprocess, 'run', child)
    args = SimpleNamespace(execute=True, bench_only=True, output=output, reference=reference)
    if failure_at is None:
        module.run(args)
    else:
        with pytest.raises(RuntimeError, match='failed'):
            module.run(args)
    state = json.loads((output/'execution.json').read_text())
    assert state['passed'] is False
    assert len(called) == (3 if failure_at is None else failure_at+1)
    assert state['stage'] == ('awaiting_visual_review' if failure_at is None else 'failed')
    assert not (output/'cases.json').exists()
    with pytest.raises(FileExistsError):
        module.run(args)


def reference_fixture(tmp_path):
    root, reference = tmp_path/'repo', tmp_path/'reference'
    root.mkdir()
    reference.mkdir()
    (root/'code.py').write_text('frozen-source\n')
    config = root/'map.yaml'
    config.write_text('image: '+str(root/'.local/hil/runtime/test/map.pgm')+'\n')
    history = reference/'history.json'
    history.write_text('actual-archived-evidence')
    baseline = dict(schema=1, source_files={'code.py':module.sha(root/'code.py')},
        configuration_files={'map.yaml':module.configuration_sha(config, root)},
        historical_recovery=dict(file='history.json', sha256=module.sha(history)))
    (reference/'baseline.json').write_text(json.dumps(baseline))
    return root, reference


@pytest.mark.parametrize('changed', ['code.py', 'map.yaml', 'history.json'])
def test_reference_rejects_code_map_or_history_drift(tmp_path, changed):
    root, reference = reference_fixture(tmp_path)
    module.verify_reference(reference, root)
    path = reference/changed if changed == 'history.json' else root/changed
    path.write_text('changed')
    with pytest.raises(ValueError, match='differs|changed'):
        module.verify_reference(reference, root)


def test_prefix_normalization_is_portable_but_does_not_normalize_binary_maps(tmp_path):
    first, second = tmp_path/'first', tmp_path/'second'
    for root in (first, second):
        root.mkdir()
        (root/'nav.yaml').write_text('file: '+str(root/'.local/hil/runtime/test/map.pgm'))
        (root/'map.pgm').write_bytes(str(root).encode())
    assert module.configuration_sha(first/'nav.yaml', first) == module.configuration_sha(second/'nav.yaml', second)
    assert module.configuration_sha(first/'map.pgm', first) != module.configuration_sha(second/'map.pgm', second)
    with pytest.raises(ValueError, match='escapes'):
        module.inside(first, '../second/map.pgm')


def test_finalize_requires_explicit_visual_reviews_before_running_audit(tmp_path, monkeypatch):
    reference, output = tmp_path/'ref', tmp_path/'run'
    reference.mkdir()
    output.mkdir()
    (reference/'baseline.json').write_text('{}')
    (output/'execution.json').write_text(json.dumps(dict(stage='awaiting_visual_review',
        reference_baseline_sha256=module.sha(reference/'baseline.json'))))
    monkeypatch.setattr(module, 'verify_reference', lambda *a: ({}, tmp_path/'historical.json'))
    monkeypatch.setattr(module.subprocess, 'run', lambda *a, **k: pytest.fail('Audit before visual review'))
    with pytest.raises(ValueError, match='visual review'):
        module.finalize(SimpleNamespace(reference=reference, output=output))
    assert not (output/'cases.json').exists()


def test_saved_casebook_covers_all_thirty_original_criteria(tmp_path):
    requirements = ROOT/'docs/deliverables/20261116_hil_existing_conditions/scope_v21/requirements.json'
    sections = json.loads(requirements.read_text())['sections']
    records = [dict(id=s['test_prefix']+'-'+c['letter'], evidence_mode='unit-test-only',
        observed='synthetic parser fixture, not physical evidence', verdict='not-executed', evidence=[])
        for s in sections for c in s['cases']]
    output = tmp_path/'casebook.txt'
    module.casebook(requirements, {'records':records}, output)
    text = output.read_text()
    assert len(records) == 30
    for section in sections:
        for case in section['cases']:
            assert section['test_prefix']+'-'+case['letter'] in text
            assert case['acceptance'] in text
    with pytest.raises(FileExistsError):
        module.casebook(requirements, {'records':records}, output)


def test_recorders_are_versioned_passive_and_not_loaded_from_old_validation_cache():
    scripts = ROOT/'docs/deliverables/20261116_hil_retest'
    assert '.validation/' not in (scripts/'observe_run.py').read_text()
    for name in ('passive_apple.py', 'passive_profiles.py'):
        tree = ast.parse((scripts/name).read_text())
        calls = [n.func.attr for n in ast.walk(tree) if isinstance(n, ast.Call) and isinstance(n.func, ast.Attribute)]
        assert not {'create_publisher', 'send', 'sendall', 'sendto'} & set(calls)
    assert sum(len(names) for _, names in module.VISUALS.values()) == 7
