import ast
from pathlib import Path


SOURCE = Path(__file__).parents[2]/'docs/deliverables/20261116_hil_retest/work_channel_rejection.py'
tree = ast.parse(SOURCE.read_text())
scope = {}
exec(compile(ast.Module(body=[node for node in tree.body if isinstance(node, ast.FunctionDef)
                            and node.name == 'settled_work_window'], type_ignores=[]), str(SOURCE), 'exec'), scope)
settled = scope['settled_work_window']


def rows(count=41):
    return [dict(sim_time=i*.05, value=dict(velocity=[10. if i < 24 else .01])) for i in range(count)]


def test_braking_transient_is_not_mistaken_for_final_nonstop():
    assert settled(rows(), 1)


def test_real_nonstop_still_fails():
    values = rows()
    values[-1]['value']['velocity'] = [1.]
    assert not settled(values, 1)


def test_missing_full_stable_window_or_feedback_fails():
    assert not settled(rows(7), 1)
    values = rows()
    del values[-8:-2]
    assert not settled(values, 1)


def test_wrong_joint_count_still_fails():
    assert not settled(rows(), 2)
