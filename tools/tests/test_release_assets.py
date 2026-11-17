"""Exercise release integrity, safe extraction, local edits and independent Git identity."""
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
from types import SimpleNamespace
import tarfile

import pytest

sys.path.insert(0, str(Path(__file__).parents[1]))
import release_assets as assets
from package_release_assets import lfs_records
from runtime_fingerprint import fingerprint


def sha(data):
    return hashlib.sha256(data).hexdigest()


def bundle(root, data=b'complete mesh data', names=None, member_name=None, symlink=False, object_sha=None):
    names = names or ['unity/model.bytes', 'ros2_ws/src/models/model.obj']
    oid = object_sha or sha(data)
    raw = io.BytesIO()
    with tarfile.open(fileobj=raw, mode='w') as archive:
        member = tarfile.TarInfo(member_name or 'objects/' + oid)
        member.size = len(data)
        if symlink:
            member.type, member.linkname, member.size = tarfile.SYMTYPE, '/tmp/should-not-be-written', 0
        archive.addfile(member, None if symlink else io.BytesIO(data))
    directory = root / '.local/bundles'
    directory.mkdir(parents=True, exist_ok=True)
    package = subprocess.check_output(['zstd', '-q', '-c'], input=raw.getvalue())
    name = 'test-assets.tar.zst'
    (directory / name).write_bytes(package)
    manifest = dict(format='agribot.release-assets', version=1, release='test-v1',
                    base_url='https://github.com/example/repository/releases/download/test-v1/',
                    files=[dict(path=n, sha256=oid, bytes=len(data), mode=0o644) for n in names],
                    bundles=[dict(name=name, bytes=len(package), sha256=sha(package), objects=[oid])])
    (root / 'resources').mkdir(exist_ok=True)
    (root / assets.MANIFEST).write_text(json.dumps(manifest))
    return manifest, directory


def test_deduplicated_bundle_restores_all_paths_without_shared_writable_inodes(tmp_path):
    manifest, directory = bundle(tmp_path)
    assert assets.restore(tmp_path, directory)['restored'] == 2
    files = [tmp_path / r['path'] for r in manifest['files']]
    assert all(p.read_bytes() == b'complete mesh data' for p in files)
    assert files[0].stat().st_ino != files[1].stat().st_ino
    assert assets.inspect(tmp_path, hashes=True) == []
    assert assets.restore(tmp_path, tmp_path/'no-bundles-needed')['restored'] == 0


def test_existing_matching_object_can_fill_another_path_without_downloading(tmp_path):
    manifest, _ = bundle(tmp_path)
    first = tmp_path / manifest['files'][0]['path']
    first.parent.mkdir(parents=True)
    first.write_bytes(b'complete mesh data')
    assert assets.restore(tmp_path, tmp_path/'not-present')['restored'] == 1


def test_legacy_pointer_is_restored(tmp_path):
    manifest, directory = bundle(tmp_path)
    target = tmp_path / manifest['files'][0]['path']
    target.parent.mkdir(parents=True)
    target.write_text('version https://git-lfs.github.com/spec/v1\noid sha256:' + '1'*64 + '\nsize 1\n')
    assets.restore(tmp_path, directory)
    assert target.read_bytes() == b'complete mesh data'


def test_local_edits_are_preserved_unless_repair_is_explicit(tmp_path):
    manifest, directory = bundle(tmp_path)
    assets.restore(tmp_path, directory)
    target = tmp_path / manifest['files'][0]['path']
    target.write_bytes(b'local edits')
    with pytest.raises(ValueError, match='preserved'):
        assets.restore(tmp_path, directory)
    assert target.read_bytes() == b'local edits'
    assets.restore(tmp_path, directory, repair=True)
    assert target.read_bytes() == b'complete mesh data'


def test_previously_managed_unedited_version_can_be_updated(tmp_path):
    _, directory = bundle(tmp_path)
    assets.restore(tmp_path, directory)
    bundle(tmp_path, data=b'new model version')
    assert assets.restore(tmp_path, directory)['restored'] == 2
    assert assets.inspect(tmp_path, hashes=True) == []


@pytest.mark.parametrize('name', ['../outside', '/absolute', 'unity/../outside', 'unity/.git/config',
                                  'unity\\outside', 'private/secret', 'unity//duplicate'])
def test_unsafe_resource_paths_are_rejected(tmp_path, name):
    bundle(tmp_path, names=[name])
    with pytest.raises(ValueError):
        assets.restore(tmp_path, tmp_path/'.local/bundles')


def test_resource_parent_symlink_cannot_escape_checkout(tmp_path):
    _, directory = bundle(tmp_path)
    (tmp_path / 'unity').symlink_to(tmp_path.parent, target_is_directory=True)
    with pytest.raises(ValueError, match='escapes'):
        assets.restore(tmp_path, directory)


def test_corrupt_bundle_is_rejected_before_any_installation(tmp_path):
    manifest, directory = bundle(tmp_path)
    (directory / manifest['bundles'][0]['name']).write_bytes(b'broken')
    with pytest.raises(ValueError, match='corrupt'):
        assets.restore(tmp_path, directory)
    assert not (tmp_path/'unity/model.bytes').exists()


@pytest.mark.parametrize('change', ['traversal', 'symlink', 'wrong-object-hash'])
def test_unsafe_or_inconsistent_archive_does_not_escape(tmp_path, change):
    kwargs = dict(member_name='../outside') if change == 'traversal' else dict(symlink=True) if change == 'symlink' else dict(object_sha='1'*64)
    _, directory = bundle(tmp_path, **kwargs)
    with pytest.raises(ValueError):
        assets.restore(tmp_path, directory)
    assert not (tmp_path/'unity/model.bytes').exists()
    assert not (tmp_path/'outside').exists()


def test_manifest_requires_every_object_to_have_exactly_one_bundle(tmp_path):
    manifest, _ = bundle(tmp_path)
    manifest['bundles'] = []
    (tmp_path/assets.MANIFEST).write_text(json.dumps(manifest))
    with pytest.raises(ValueError, match='no release bundle'):
        assets.load_manifest(tmp_path)


class Response(io.BytesIO):
    def __init__(self, data, code=200, headers=None):
        super().__init__(data)
        self.code, self.headers = code, headers or {}

    def getcode(self):
        return self.code


@pytest.mark.parametrize('resume', ['fresh', 'range', 'server-ignores-range'])
def test_download_verifies_full_hash_and_handles_resume(tmp_path, monkeypatch, resume):
    payload = b'1234567890'
    target = tmp_path/'bundle.tar.zst'
    requests = []
    if resume != 'fresh':
        target.with_suffix('.zst.part').write_bytes(payload[:4])
    def open_request(request, timeout):
        requests.append(request)
        return Response(payload[4:], 206, {'Content-Range': 'bytes 4-9/10'}) if resume == 'range' else Response(payload)
    monkeypatch.setattr(assets.urllib.request, 'build_opener', lambda *a: SimpleNamespace(open=open_request))
    assets.download('https://github.com/o/r/releases/download/v1/bundle.tar.zst', target, len(payload), sha(payload))
    assert target.read_bytes() == payload
    assert bool(requests[0].get_header('Range')) == (resume != 'fresh')
    assert not target.with_suffix('.zst.part').exists()


def test_wrong_download_hash_does_not_replace_existing_file(tmp_path, monkeypatch):
    target = tmp_path/'bundle.tar.zst'
    target.write_bytes(b'old')
    monkeypatch.setattr(assets.urllib.request, 'build_opener',
                        lambda *a: SimpleNamespace(open=lambda *a, **k: Response(b'bad')))
    with pytest.raises(ValueError, match='SHA-256'):
        assets.download('https://github.com/o/r/releases/download/v/bundle', target, 3, sha(b'new'), attempts=1)
    assert target.read_bytes() == b'old'


@pytest.mark.parametrize('url', ['http://github.com/a', 'https://other.example/a',
                                 'https://github.com@other.example/a', 'file:///tmp/a'])
def test_download_rejects_non_github_or_insecure_urls(url):
    with pytest.raises(ValueError):
        assets.validate_url(url)


def test_restored_ignored_resources_are_bound_to_the_runtime_identity(tmp_path):
    manifest, directory = bundle(tmp_path)
    git = lambda *a: subprocess.check_output(['git', '-C', str(tmp_path), *a])
    git('init', '-q')
    git('config', 'user.name', 'Release Test')
    git('config', 'user.email', 'test@example.invalid')
    (tmp_path/'.gitignore').write_text('.local/\n/unity/model.bytes\n/ros2_ws/src/models/model.obj\n')
    git('add', '.gitignore', 'resources/release_assets.json')
    git('commit', '-qm', 'source and resource lock only')
    with pytest.raises(ValueError, match='发布资源未就绪'):
        fingerprint(tmp_path)
    assets.restore(tmp_path, directory)
    before = fingerprint(tmp_path)
    assert not git('status', '--porcelain')
    target = tmp_path/manifest['files'][0]['path']
    target.write_bytes(b'changed')
    with pytest.raises(ValueError, match='未登记修改'):
        fingerprint(tmp_path)
    assets.restore(tmp_path, directory, repair=True)
    assert fingerprint(tmp_path)['sha256'] == before['sha256']


def test_legacy_inventory_uses_git_pointer_data_without_lfs_client(tmp_path):
    git = lambda *a, **k: subprocess.check_output(['git','-C',str(tmp_path),*a],**k)
    git('init','-q')
    git('config','user.name','Release Test')
    git('config','user.email','test@example.invalid')
    path = tmp_path/'unity'
    path.mkdir()
    payload = 'version https://git-lfs.github.com/spec/v1\noid sha256:' + 'a'*64 + '\nsize 12\n'
    (path/'model.bytes').write_text(payload)
    git('add','unity')
    git('commit','-qm','pointer fixture')
    assert lfs_records(tmp_path) == [dict(path='unity/model.bytes',sha256='a'*64,bytes=12,mode=0o644)]
