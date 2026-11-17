import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tarfile

import pytest


sys.path.insert(0, str(Path(__file__).parents[1]))
spec = importlib.util.spec_from_file_location('package_app', Path(__file__).parents[1] / 'package_app.py')
package = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package)


@pytest.mark.parametrize('link', [False, True])
def test_package_contains_real_player_even_when_dist_is_a_symlink(tmp_path, monkeypatch, link):
    app = tmp_path / ('.local' if link else 'dist') / 'unity'
    app.mkdir(parents=True)
    for name in ('AgribotConfigurator.x86_64', 'libagribot_linux_ime.so', 'start_agribot_configurator.sh'):
        (app / name).write_bytes(b'player fixture: ' + name.encode())
    inputs = {'sha256': 'test-runtime-fingerprint'}
    (app / 'build-info.json').write_text(json.dumps(inputs))
    if link:
        (tmp_path / 'dist').mkdir()
        (tmp_path / 'dist/unity').symlink_to(app, target_is_directory=True)
    monkeypatch.setattr(package, 'ROOT', tmp_path)
    monkeypatch.setattr(package, 'fingerprint', lambda root: inputs)
    url = 'https://github.com/example/repository/releases/download/ui-v2/'
    monkeypatch.setattr('sys.argv', ['package_app.py', '--app', str(tmp_path / 'dist/unity'),
                                   '--release-base-url', url])
    package.main()
    manifest = json.loads((tmp_path / 'prebuilt/manifest.json').read_text())
    archive = tmp_path / 'prebuilt' / manifest['archive']
    assert manifest['url'] == url + archive.name
    assert manifest['bytes'] == archive.stat().st_size
    assert manifest['sha256'] == package.digest(archive)
    data = subprocess.check_output(['zstd', '-dc', str(archive)])
    with tarfile.open(fileobj=io.BytesIO(data)) as stream:
        assert all(member.isfile() or member.isdir() for member in stream)
        binary = stream.extractfile('unity/AgribotConfigurator.x86_64').read()
    assert binary == (app / 'AgribotConfigurator.x86_64').read_bytes()
