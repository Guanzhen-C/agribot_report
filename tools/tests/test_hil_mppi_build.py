from pathlib import Path
from types import SimpleNamespace
import sys

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]/'hil'))
import prepare_runtime as preparation


def test_pinned_mppi_dependency_uses_a_shallow_scoped_checkout(tmp_path, monkeypatch):
    calls = []
    monkeypatch.setattr(preparation, 'run', lambda argv, **_: calls.append(list(map(str, argv))))
    monkeypatch.setattr(preparation.subprocess, 'check_output', lambda *_a, **_k: preparation.NAV2+'\n')
    preparation.dependency(tmp_path/'new', 'https://github.com/ros-navigation/navigation2.git', preparation.NAV2,
                           ref='1.1.20', sparse=('nav2_mppi_controller',))
    assert calls[0][:8] == ['git', 'clone', '--filter=blob:none', '--no-checkout', '--depth', '1', '--branch', '1.1.20']
    assert calls[1][-4:] == ['sparse-checkout', 'set', '--cone', 'nav2_mppi_controller']
    assert calls[2][-3:] == ['checkout', '--detach', preparation.NAV2]


def test_wrong_dependency_revision_is_rejected_before_patching(tmp_path, monkeypatch):
    monkeypatch.setattr(preparation.subprocess, 'check_output', lambda *_a, **_k: 'unexpected\n')
    monkeypatch.setattr(preparation, 'run', lambda *_a, **_k: pytest.fail('Do not patch a different upstream revision'))
    with pytest.raises(ValueError, match='Dependency revision differs'):
        preparation.dependency(tmp_path, 'unused', preparation.NAV2, patch='unused.patch')


def test_patch_changes_only_the_known_exception_not_collision_or_retry_limits():
    root = Path(__file__).resolve().parents[2]
    patch = (root/'ros2_ws/src/agribot_hil_apple/patches/nav2_humble_mppi_failure.patch').read_text()
    added = [line for line in patch.splitlines() if line.startswith('+') and not line.startswith('+++')]
    assert added == ['+#include "nav2_core/exceptions.hpp"',
                     '+    throw nav2_core::PlannerException("Optimizer fail to compute path");']
    assert preparation.NAV2 == 'a097086719c88f781aa59788eca29ac6ca5e56db'
