import importlib.util
from pathlib import Path


path = Path(__file__).resolve().parents[2]/'docs/deliverables/20261116_hil_retest/harrow_history.py'
spec = importlib.util.spec_from_file_location('hil_harrow_history_tested', path)
history = importlib.util.module_from_spec(spec)
spec.loader.exec_module(history)


def fixture():
    a = [0., 0., .015, 0., 0., 0., 1.]
    b = [.3, 0., .015, 0., 0., 0., 1.]
    rows = [dict(kind='history', sim_time=0., poses=[])]
    for index in range(41):
        stamp = index*.05
        rows.append(dict(kind='plant', sim_time=stamp,
            values=[9000., 0., int(stamp <= .5), -.05 if stamp <= .5 else .5, .2, 0., 0., 0., stamp]))
    rows += [dict(kind='history', sim_time=s, poses=[a, b]) for s in (.5, 1., 1.5, 2.)]
    return rows


def test_real_growth_and_raised_history_are_separate():
    result = history.analyze(fixture())
    assert result['passed']
    assert result['maximum_segments'] == 1
    assert result['raised_history_preserved_sim_seconds'] == 1.


def test_nonworking_growth_is_rejected():
    rows = fixture()
    rows[-1]['poses'] += rows[-1]['poses'][:]
    assert not history.analyze(rows)['passed']


def test_disappearing_history_is_rejected():
    rows = fixture()
    rows[-1]['poses'] = []
    assert not history.analyze(rows)['passed']


def test_no_history_and_missing_plant_coverage_are_not_passes():
    assert not history.analyze([])['passed']
    assert not history.analyze([r for r in fixture() if r['kind'] != 'plant'])['passed']
