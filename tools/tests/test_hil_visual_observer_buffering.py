import ast
from pathlib import Path


def test_small_gazebo_echo_is_line_buffered_without_relaxing_freshness():
    source = (Path(__file__).resolve().parents[2]/
        'docs/deliverables/20261116_hil_retest/spray_visual_checks.py').read_text()
    tree = ast.parse(source)
    calls = [n for n in ast.walk(tree) if isinstance(n, ast.Call) and
             isinstance(n.func, ast.Attribute) and n.func.attr == 'Popen']
    assert len(calls) == 1
    command = calls[0].args[0]
    assert isinstance(command, ast.List)
    assert [n.value for n in command.elts[:6]] == ['stdbuf', '-oL', '-eL', 'gz', 'topic', '-u']
    assert "row['received_monotonic'] > 1." in source
    assert "snapshot('spray')[0] == 0." in source
