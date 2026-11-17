from pathlib import Path
import runpy


def run_review(off=False, gap=.05, source_passed=True, missing_pc=False):
    source = Path(__file__).resolve().parents[2]/'docs/deliverables/20261116_hil_retest/review_powertrain.py'
    review = runpy.run_path(str(source))['review']
    def state(t, mode=2, speed=.4):
        values = [0.]*20
        values[0], values[10], values[17], values[19] = mode, speed, t, float(mode != 0)
        return values
    samples = [dict(at=t+100, commanded_transmission='powershift_auto', powertrain=state(t)) for t in (0., 5., 10.)]
    samples.append(dict(at=111., commanded_transmission='off', powertrain=state(11., 0)))
    pc = [[t, t, state(t, 0 if off and t == 5. else 2)] for t in (0., 5., 10.)]
    pc.append([11., 11., state(11., 0)])
    events = [dict(at=t, event=dict(kind='powertrain_status')) for t in (104., 104.05, 104.05+gap)]
    return review(dict(audit_passed=source_passed, unplanned_off_samples=0), samples,
                  dict(samples=dict(power=[] if missing_pc else pc)), events)


def test_pc_observation_catches_an_off_event_missing_from_x5_samples():
    result = run_review(off=True)
    assert result['original_x5_audit_passed']
    assert result['x5_unplanned_off_samples'] == 0
    assert result['pc_unplanned_off_while_moving_count'] == 1
    assert not result['passed']


def test_commanded_final_disarm_is_not_misclassified_as_unplanned_off():
    assert run_review()['passed']


def test_existing_point_three_second_gate_is_not_relaxed():
    assert not run_review(gap=.31)['passed']


def test_missing_independent_data_or_original_failure_cannot_become_a_pass():
    assert not run_review(missing_pc=True)['passed']
    assert not run_review(source_passed=False)['passed']
