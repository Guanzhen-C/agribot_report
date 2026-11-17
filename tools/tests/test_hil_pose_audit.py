import runpy
from pathlib import Path


def analysis():
    return runpy.run_path(str(Path(__file__).resolve().parents[2]/
        'docs/deliverables/20261116_hil_retest/pose_audit.py'))


def test_simulation_stamp_interpolation_not_cross_host_wall_clock():
    rows = [dict(kind='truth', stamp=t, xy=[t, 0.], at=100.+t) for t in [0., .1, .2]]
    rows += [dict(kind='localization', stamp=.05, xy=[.05, .2], at=5000., ready=True),
             dict(kind='localization', stamp=.15, xy=[.15, .3], at=5000., ready=True)]
    report = analysis()['analyze'](rows)
    assert report['localization_match_coverage'] == 1.
    assert report['localization_xy']['maximum_m'] == .3
    assert report['maximum_truth_interpolation_gap_sim_seconds'] == .1


def test_missing_truth_or_wide_brackets_are_reported_not_silently_matched():
    rows = [dict(kind='truth', stamp=0., xy=[0., 0.]), dict(kind='truth', stamp=2., xy=[2., 0.]),
            dict(kind='localization', stamp=1., xy=[1., 0.], ready=True)]
    report = analysis()['analyze'](rows)
    assert report['localization_match_coverage'] == 0.
    assert len(report['unmatched_localization']) == 1


def test_path_distance_is_to_segments_not_only_vertices():
    values = analysis()['distances_to_path']([[5., 2.]], [[0., 0.], [10., 0.]])
    assert values == [2.]
