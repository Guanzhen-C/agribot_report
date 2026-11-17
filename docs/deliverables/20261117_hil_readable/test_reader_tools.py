"""Offline fixtures only: do not connect to a HIL bench."""
import importlib.util
import json
from pathlib import Path

import pytest


HERE = Path(__file__).resolve().parent


def module(name):
    spec = importlib.util.spec_from_file_location(name, HERE / (name + '.py'))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


viewer = module('inspect_results')
review = module('review_images')


@pytest.fixture
def ledger():
    return dict(passed=True, requirements_version='V2.1', records=[dict(id=identity, title='离线夹具', verdict='通过',
        evidence_mode='测试数据，不是实板', observed='仅测试读取', evidence=[])
        for identity in viewer.IDS])


def test_all_aliases_map_to_the_frozen_thirty_cases():
    assert [viewer.case_id(f'YL-FU-{number:03d}') for number in range(1, 31)] == viewer.IDS
    with pytest.raises(ValueError):
        viewer.case_id('YL-FU-031')


def test_history_is_explicit_and_does_not_claim_fresh_execution(tmp_path, ledger, capsys):
    path = tmp_path / 'history.json'
    path.write_text(json.dumps(ledger))
    assert viewer.main(['--ledger', str(path), '--case', 'YL-FU-029']) == 0
    output = capsys.readouterr().out
    assert '历史归档结果，不代表刚刚又执行' in output
    assert '不是本轮实板故障注入' in output
    assert 'TC-HIL-NAV-001-i' in output


def test_awaiting_review_cannot_reuse_an_existing_pass(tmp_path, ledger, capsys):
    (tmp_path / 'cases.json').write_text(json.dumps(ledger))
    (tmp_path / 'execution.json').write_text(json.dumps(dict(stage='awaiting_visual_review', passed=False)))
    assert viewer.main(['--output', str(tmp_path)]) == 2
    assert '当前不能给出30条最终通过结论' in capsys.readouterr().out


@pytest.mark.parametrize('fault', ['case_failed', 'ledger_failed', 'execution_failed', 'duplicate_id', 'wrong_scope'])
def test_inconsistent_final_records_never_pass(tmp_path, ledger, fault):
    state = dict(stage='finalized', passed=True)
    if fault == 'case_failed':
        ledger['records'][0]['verdict'] = '不通过'
    if fault == 'ledger_failed':
        ledger['passed'] = False
    if fault == 'execution_failed':
        state['passed'] = False
    if fault == 'duplicate_id':
        ledger['records'][-1]['id'] = ledger['records'][0]['id']
    if fault == 'wrong_scope':
        ledger['requirements_version'] = 'V2.0'
    (tmp_path / 'cases.json').write_text(json.dumps(ledger))
    (tmp_path / 'execution.json').write_text(json.dumps(state))
    assert viewer.main(['--output', str(tmp_path)]) == 2


@pytest.fixture
def captures(tmp_path):
    directory = tmp_path / 'spray-existing'
    directory.mkdir()
    (tmp_path / 'controller-checks.json').write_text(json.dumps(dict(passed=True, run_id='offline_fixture')))
    (directory / 'result.json').write_text(json.dumps(dict(passed=True)))
    for name in review.NAMES['spray']:
        path = directory / name
        path.write_bytes(b'\x89PNG\r\n\x1a\nfixture only')
        path.with_suffix('.xwd').write_bytes(b'offline capture fixture')
    return directory


def test_human_review_preserves_identity_and_binds_exact_captures(captures):
    value = review.register(captures, 'spray', '测试人员甲', '逐图实际观察的离线测试文字', True)
    assert value['reviewer'] == '测试人员甲'
    assert value['reviewer_type'] == 'named_human'
    assert len(value['images_sha256']) == len(value['original_capture_sha256']) == 3
    assert value['run_id'] == 'offline_fixture'
    with pytest.raises(FileExistsError):
        review.register(captures, 'spray', '另一人', '禁止覆盖前一次登记', True)


def test_human_review_is_accepted_by_the_existing_frozen_evidence_audit(captures):
    value = review.register(captures, 'spray', '人工审阅者', '离线格式兼容性检查', True)
    source = HERE.parent / '20261116_hil_existing_conditions/audit_fresh.py'
    spec = importlib.util.spec_from_file_location('existing_visual_audit', source)
    audit = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(audit)
    assert len(audit.verify_visual_review(captures, value, 'offline_fixture', 3)) == 6
    (captures / 'pump-stopped.png').write_bytes(b'changed offline fixture')
    with pytest.raises(ValueError):
        audit.verify_visual_review(captures, value, 'offline_fixture', 3)


@pytest.mark.parametrize('name,notes,approve', [('', '观察', True), ('甲', '', True), ('甲', '观察', False)])
def test_review_requires_name_notes_and_explicit_confirmation(captures, name, notes, approve):
    with pytest.raises(ValueError):
        review.register(captures, 'spray', name, notes, approve)
    assert not (captures / 'visual-review.json').exists()


def test_failed_physical_execution_cannot_be_approved(captures):
    (captures / 'result.json').write_text(json.dumps(dict(passed=False)))
    with pytest.raises(ValueError):
        review.register(captures, 'spray', '甲', '有图不代表执行通过', True)


def test_missing_original_capture_cannot_be_approved(captures):
    (captures / 'original-nozzles.xwd').unlink()
    with pytest.raises(FileNotFoundError):
        review.register(captures, 'spray', '甲', '缺证据', True)
    assert not (captures / 'visual-review.json').exists()
