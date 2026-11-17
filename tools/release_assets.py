"""Versioned, hash-checked release assets, independent of Git LFS billing."""
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import tarfile
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request

MANIFEST = 'resources/release_assets.json'
OID = re.compile(r'^[0-9a-f]{64}$')
ROOTS = ('unity/', 'ros2_ws/src/', 'resources/')


def digest(path):
    result = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024**2), b''):
            result.update(block)
    return result.hexdigest()


def safe_target(root, name):
    if not isinstance(name, str):
        raise ValueError('Resource path must be a string')
    path = PurePosixPath(name)
    if (not name or '\\' in name or path.is_absolute()
            or any(part in ('', '.', '..', '.git') for part in name.split('/'))
            or not name.startswith(ROOTS)):
        raise ValueError('Unsafe resource path: ' + str(name))
    root = Path(root).resolve()
    target = root / path
    if target.is_symlink() or not target.resolve().is_relative_to(root):
        raise ValueError('Resource path escapes the checkout: ' + name)
    return target


def validate_url(url):
    parsed = urllib.parse.urlsplit(url)
    if (parsed.scheme != 'https' or parsed.hostname not in
            ('github.com', 'release-assets.githubusercontent.com', 'objects.githubusercontent.com')
            or parsed.username or parsed.password):
        raise ValueError('Only HTTPS GitHub release downloads are allowed')
    return url


class _HttpsRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, new_url):
        validate_url(new_url)
        return super().redirect_request(request, response, code, message, headers, new_url)


def download(url, destination, size, sha256, attempts=3):
    validate_url(url)
    if not isinstance(size, int) or size <= 0 or not OID.fullmatch(sha256):
        raise ValueError('Invalid download size or SHA-256')
    destination = Path(destination)
    if destination.is_file() and destination.stat().st_size == size and digest(destination) == sha256:
        return destination
    destination.parent.mkdir(parents=True, exist_ok=True)
    partial = destination.with_name(destination.name + '.part')
    if destination.is_symlink() or partial.is_symlink():
        raise ValueError('Download destination must not be a symlink')
    opener = urllib.request.build_opener(_HttpsRedirect())
    for attempt in range(attempts):
        try:
            offset = partial.stat().st_size if partial.is_file() else 0
            if offset == size and digest(partial) == sha256:
                os.replace(partial, destination)
                return destination
            if offset >= size:
                partial.unlink()
                offset = 0
            headers = {'User-Agent': 'agribot-report-installer/1', 'Accept': 'application/octet-stream'}
            if offset:
                headers['Range'] = 'bytes=' + str(offset) + '-'
            with opener.open(urllib.request.Request(url, headers=headers), timeout=90) as response:
                status = response.getcode()
                if status == 206:
                    expected = r'bytes ' + str(offset) + r'-\d+/' + str(size)
                    if not re.fullmatch(expected, response.headers.get('Content-Range', '')):
                        raise ValueError('Invalid resumed download Content-Range')
                elif status == 200:
                    offset = 0
                else:
                    raise ValueError('Unexpected download status: ' + str(status))
                with partial.open('ab' if offset else 'wb') as output:
                    written = offset
                    for block in iter(lambda: response.read(4 * 1024**2), b''):
                        written += len(block)
                        if written > size:
                            raise ValueError('Download exceeds the manifest size')
                        output.write(block)
            if partial.stat().st_size != size:
                raise OSError('Incomplete release download')
            if digest(partial) != sha256:
                partial.unlink()
                raise ValueError('Release download SHA-256 mismatch')
            os.replace(partial, destination)
            return destination
        except (OSError, ValueError, urllib.error.URLError):
            if attempt + 1 == attempts:
                raise
            time.sleep(min(2 ** attempt, 8))
    raise RuntimeError('Download did not finish')


def load_manifest(root):
    root = Path(root)
    manifest = json.loads((root / MANIFEST).read_text())
    if manifest.get('format') != 'agribot.release-assets' or manifest.get('version') != 1:
        raise ValueError('Unsupported resource manifest')
    validate_url(manifest['base_url'])
    if not manifest['base_url'].endswith('/'):
        raise ValueError('Release base URL must end with a slash')
    paths, objects, covered, bundles = set(), {}, set(), set()
    for record in manifest['files']:
        name, oid, size = record['path'], record['sha256'], record['bytes']
        safe_target(root, name)
        if (name in paths or not OID.fullmatch(oid) or type(size) is not int or size <= 0
                or record['mode'] not in (0o644, 0o755)):
            raise ValueError('Invalid or duplicate resource record: ' + name)
        if oid in objects and objects[oid] != size:
            raise ValueError('Conflicting object sizes')
        paths.add(name)
        objects[oid] = size
    for bundle in manifest['bundles']:
        name = bundle['name']
        if (PurePosixPath(name).name != name or '\\' in name or name in bundles
                or not name.endswith('.tar.zst') or bundle['bytes'] <= 0
                or not OID.fullmatch(bundle['sha256'])):
            raise ValueError('Invalid resource bundle')
        bundles.add(name)
        for oid in bundle['objects']:
            if oid not in objects or oid in covered:
                raise ValueError('Unrecognized or duplicate bundled object')
            covered.add(oid)
    if covered != set(objects):
        raise ValueError('Some resource objects have no release bundle')
    return manifest


def inspect(root, hashes=False):
    root = Path(root)
    if not (root / MANIFEST).exists():
        return []
    try:
        manifest = load_manifest(root)
    except (ValueError, KeyError, TypeError) as error:
        return ['Release resource manifest: ' + str(error)]
    issues = []
    for record in manifest['files']:
        target = safe_target(root, record['path'])
        if not target.is_file() or target.stat().st_size != record['bytes']:
            issues.append('Missing/incomplete release resource: ' + record['path'])
        elif hashes and digest(target) != record['sha256']:
            issues.append('Modified release resource: ' + record['path'])
    return issues


def _copy_atomic(source, target, mode):
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(prefix='.agribot-', dir=target.parent, delete=False) as stream:
            temporary = Path(stream.name)
            with Path(source).open('rb') as original:
                shutil.copyfileobj(original, stream, 4 * 1024**2)
        temporary.chmod(mode)
        os.replace(temporary, target)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def restore(root, bundle_dir=None, repair=False):
    root = Path(root).resolve()
    manifest = load_manifest(root)
    state = root / '.local/release-assets-installed.json'
    previous = json.loads(state.read_text()).get('files', {}) if state.is_file() else {}
    missing, known = {}, {}
    for record in manifest['files']:
        name, oid = record['path'], record['sha256']
        path = safe_target(root, name)
        if path.exists() and not path.is_file():
            raise ValueError('Resource target is not a regular file: ' + name)
        actual = digest(path) if path.is_file() else None
        if actual == oid:
            known[oid] = path
            continue
        pointer = False
        if path.is_file():
            with path.open('rb') as stream:
                pointer = stream.read(80).startswith(b'version https://git-lfs.github.com/spec/v1')
        if path.exists() and not pointer and not repair and actual != previous.get(name):
            raise ValueError('Local resource has edits; preserved: ' + name + '. Use --repair only to discard these edits.')
        missing.setdefault(oid, []).append(record)

    restored = 0
    for oid in list(missing):
        if oid not in known:
            continue
        for record in missing.pop(oid):
            _copy_atomic(known[oid], safe_target(root, record['path']), record['mode'])
            restored += 1

    cache = root / '.local/release-downloads' / manifest['release']
    for bundle in manifest['bundles']:
        needed = set(bundle['objects']) & missing.keys()
        if not needed:
            continue
        archive = (Path(bundle_dir) if bundle_dir else cache) / bundle['name']
        if bundle_dir:
            if not archive.is_file() or archive.stat().st_size != bundle['bytes'] or digest(archive) != bundle['sha256']:
                raise ValueError('Missing or corrupt offline bundle: ' + str(archive))
        else:
            print('Downloading release resources: ' + bundle['name'], flush=True)
            download(manifest['base_url'] + bundle['name'], archive, bundle['bytes'], bundle['sha256'])
        sizes = {r['sha256']: r['bytes'] for r in manifest['files']}
        cache.mkdir(parents=True, exist_ok=True)
        process = subprocess.Popen(['zstd', '-dc', str(archive)], stdout=subprocess.PIPE)
        try:
            seen = set()
            with tempfile.TemporaryDirectory(prefix='.restore-', dir=cache) as directory:
                with tarfile.open(fileobj=process.stdout, mode='r|') as package:
                    for member in package:
                        oid = member.name.removeprefix('objects/')
                        if (member.name != 'objects/' + oid or not OID.fullmatch(oid)
                                or oid not in bundle['objects'] or oid in seen
                                or not member.isfile() or member.size != sizes[oid]):
                            raise ValueError('Unsafe or inconsistent resource bundle member: ' + member.name)
                        seen.add(oid)
                        if oid not in needed:
                            continue
                        source = Path(directory) / oid
                        result = hashlib.sha256()
                        with package.extractfile(member) as stream, source.open('wb') as output:
                            for block in iter(lambda: stream.read(4 * 1024**2), b''):
                                result.update(block)
                                output.write(block)
                        if result.hexdigest() != oid:
                            raise ValueError('Extracted object SHA-256 mismatch: ' + oid)
                        for record in missing.pop(oid):
                            _copy_atomic(source, safe_target(root, record['path']), record['mode'])
                            restored += 1
                        source.unlink()
            if seen != set(bundle['objects']) or process.wait() != 0:
                raise ValueError('Incomplete resource bundle')
        finally:
            if process.stdout is not None:
                process.stdout.close()
            if process.poll() is None:
                process.terminate()
                process.wait()
    if missing:
        raise ValueError('Release did not supply all missing resource objects')
    state.parent.mkdir(parents=True, exist_ok=True)
    payload = {'release': manifest['release'], 'files': {r['path']: r['sha256'] for r in manifest['files']}}
    temporary = state.with_suffix('.tmp')
    temporary.write_text(json.dumps(payload, indent=2) + '\n')
    os.replace(temporary, state)
    return dict(files=len(manifest['files']), restored=restored, release=manifest['release'])
