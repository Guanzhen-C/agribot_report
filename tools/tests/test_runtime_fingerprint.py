"""Real Git repositories exercise the release/source identity contract."""
import importlib.util
from pathlib import Path
import subprocess

import pytest


spec = importlib.util.spec_from_file_location(
    'runtime_fingerprint', Path(__file__).parents[1] / 'runtime_fingerprint.py')
runtime = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runtime)


def git(root, *arguments):
    return subprocess.check_output(['git', '-C', str(root), *arguments], stderr=subprocess.STDOUT)


@pytest.fixture
def repository(tmp_path):
    git(tmp_path, 'init', '-q')
    git(tmp_path, 'config', 'user.name', 'Release Test')
    git(tmp_path, 'config', 'user.email', 'test@example.invalid')
    (tmp_path / 'unity').mkdir()
    (tmp_path / 'unity/source.cs').write_text('original source\n')
    (tmp_path / 'docs').mkdir()
    (tmp_path / 'docs/manual.md').write_text('manual\n')
    git(tmp_path, 'add', '.')
    git(tmp_path, 'commit', '-qm', 'initial fixture')
    return tmp_path


def test_different_checkout_path_has_same_runtime_identity(repository, tmp_path):
    before = runtime.fingerprint(repository)
    other = tmp_path / 'another-directory'
    git(repository, 'clone', '-q', '--no-hardlinks', str(repository), str(other))
    assert runtime.fingerprint(other)['sha256'] == before['sha256']


def test_documentation_commit_does_not_invalidate_the_app(repository):
    before = runtime.fingerprint(repository)
    (repository / 'docs/manual.md').write_text('updated manual\n')
    git(repository, 'add', 'docs')
    git(repository, 'commit', '-qm', 'documentation only')
    after = runtime.fingerprint(repository)
    assert after['source_commit'] != before['source_commit']
    assert after['sha256'] == before['sha256']


@pytest.mark.parametrize('state', ['working', 'staged', 'untracked'])
def test_uncommitted_runtime_input_cannot_be_released(repository, state):
    path = repository / 'unity' / ('new.cs' if state == 'untracked' else 'source.cs')
    path.write_text('modified source\n')
    if state == 'staged':
        git(repository, 'add', 'unity')
    with pytest.raises(ValueError, match='未提交修改'):
        runtime.fingerprint(repository)


def test_committed_runtime_change_invalidates_old_app(repository):
    before = runtime.fingerprint(repository)
    (repository / 'unity/source.cs').write_text('new version\n')
    git(repository, 'add', 'unity')
    git(repository, 'commit', '-qm', 'new source')
    assert runtime.fingerprint(repository)['sha256'] != before['sha256']


def test_runtime_executable_mode_is_part_of_identity(repository):
    before = runtime.fingerprint(repository)
    git(repository, 'update-index', '--chmod=+x', 'unity/source.cs')
    (repository / 'unity/source.cs').chmod(0o755)
    git(repository, 'commit', '-qm', 'executable mode')
    assert runtime.fingerprint(repository)['sha256'] != before['sha256']
