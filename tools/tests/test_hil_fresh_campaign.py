import ast
import datetime
import hashlib
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HERE = ROOT/'docs/deliverables/20261116_hil_existing_conditions'


def test_both_phases_have_distinct_fresh_missions_and_controller_jobs():
    phases = [json.loads((HERE/name).read_text()) for name in ('phase_a_jobs.json', 'phase_b_jobs.json')]
    for jobs in phases:
        for kind in ('mission', 'controller'):
            assert {j['profile'] for j in jobs if j['kind'] == kind} == {
                'ackermann_apple', 'tracked_sprayer', 'tracked_shredder', 'tractor_powershift', 'tractor_cvt'}
        assert len({j['name'] for j in jobs}) == len(jobs)
        assert not any(set(j.get('checks', [])) & {'visual', 'navigation'} for j in jobs)
    assert not {j['name'] for j in phases[0]} & {j['name'] for j in phases[1]}
    assert 'existing-spray' in next(j for j in phases[1] if j['name'] == 'b-sprayer-controls')['checks']


def test_existing_spray_cannot_insert_or_move_bodies():
    tree = ast.parse((HERE/'spray_existing.py').read_text())
    names = {n.id for n in ast.walk(tree) if isinstance(n, ast.Name)}
    strings = {n.value for n in ast.walk(tree) if isinstance(n, ast.Constant) and isinstance(n.value, str)}
    assert not names & {'SpawnEntity', 'DeleteEntity', 'SetEntityState', 'SetModelState'}
    assert not any('/spawn_entity' in s or '/set_entity_state' in s or '/set_model_state' in s for s in strings)


def test_existing_scene_hit_filter_excludes_open_rays_and_ground():
    # Load only this pure measurement helper, without importing ROS or starting a node.
    tree = ast.parse((HERE/'spray_existing.py').read_text())
    function = next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == 'blocked_rays')
    namespace = {}
    exec('import math', namespace)
    exec(compile(ast.Module(body=[function], type_ignores=[]), '<hit-filter>', 'exec'), namespace)
    nozzle = dict(size=9, tangent=.18, origin=[0., 0., 1.], direction=[1., 0., 0.],
                  u=[0., 1., 0.], v=[0., 0., 1.], ranges=[4.5]*81)
    assert namespace['blocked_rays']({'nozzles': [nozzle]}) == []
    nozzle['ranges'][40] = 1.
    hits = namespace['blocked_rays']({'nozzles': [nozzle]})
    assert len(hits) == 1 and hits[0]['hit_world'] == [1., 0., 1.]
    nozzle['origin'][2] = 0.
    assert namespace['blocked_rays']({'nozzles': [nozzle]}) == []


def test_old_hardware_identity_cannot_be_relabelled_as_fresh():
    import pytest
    tree = ast.parse((HERE/'audit_fresh.py').read_text())
    function = next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == 'fresh_identity')
    namespace = {'datetime': datetime}
    exec(compile(ast.Module(body=[function], type_ignores=[]), '<fresh-identity>', 'exec'), namespace)
    after = datetime.datetime(2026, 11, 16, 12, 23, tzinfo=datetime.timezone.utc)
    with pytest.raises(ValueError, match='Archived run'):
        namespace['fresh_identity']('20261116T115538_54d717fa', after)
    namespace['fresh_identity']('20261116T122428_f93b23b1', after)


def test_visual_review_binds_images_and_raw_captures_to_the_new_run(tmp_path):
    import pytest
    tree = ast.parse((HERE/'audit_fresh.py').read_text())
    function = next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == 'verify_visual_review')
    namespace = {'Path': Path, 'hashlib': hashlib}
    exec(compile(ast.Module(body=[function], type_ignores=[]), '<visual-binding>', 'exec'), namespace)
    image, capture = tmp_path/'frame.png', tmp_path/'frame.xwd'
    image.write_bytes(b'unit-test-image')
    capture.write_bytes(b'unit-test-raw-capture')
    review = dict(run_id='new', images_sha256={'frame.png': hashlib.sha256(image.read_bytes()).hexdigest()},
                  original_capture_sha256={'frame.xwd': hashlib.sha256(capture.read_bytes()).hexdigest()})
    verify = namespace['verify_visual_review']
    assert set(verify(tmp_path, review, 'new', 1)) == {image, capture}
    with pytest.raises(ValueError, match='different physical run'):
        verify(tmp_path, review, 'old', 1)
    with pytest.raises(ValueError, match='incomplete'):
        verify(tmp_path, review, 'new', 4)
    image.write_bytes(b'changed')
    with pytest.raises(ValueError, match='changed'):
        verify(tmp_path, review, 'new', 1)
