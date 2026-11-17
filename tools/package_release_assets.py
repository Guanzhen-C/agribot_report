#!/usr/bin/env python3
"""Build deduplicated release bundles and the tracked resource lock manifest."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tarfile

from release_assets import MANIFEST, digest, safe_target

ROOT = Path(__file__).resolve().parents[1]


def lfs_records(root):
    records = []
    entries = subprocess.check_output(['git', '-C', str(root), 'ls-tree', '-rlz', 'HEAD']).split(b'\0')
    candidates = []
    for entry in entries:
        if not entry:
            continue
        identity, name = entry.split(b'\t', 1)
        mode, kind, blob, size = identity.split()
        if kind == b'blob' and int(size) < 1024:
            candidates.append((mode, blob, name.decode()))
    process = subprocess.Popen(['git', '-C', str(root), 'cat-file', '--batch'], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    try:
        for mode, blob, name in candidates:
            process.stdin.write(blob + b'\n')
            process.stdin.flush()
            _, kind, size = process.stdout.readline().split()
            assert kind == b'blob'
            content = process.stdout.read(int(size))
            assert process.stdout.read(1) == b'\n'
            if content.startswith(b'version https://git-lfs.github.com/spec/v1\n'):
                fields = dict(line.decode().split(' ', 1) for line in content.strip().splitlines())
                records.append(dict(path=name, sha256=fields['oid'].removeprefix('sha256:'),
                                    bytes=int(fields['size']), mode=int(mode, 8) & 0o777))
    finally:
        process.stdin.close()
        process.stdout.close()
        process.wait()
    return records


def package(root, output, release, repository, maximum=1400 * 1024**2):
    root, output = Path(root), Path(output)
    if '/' in release or not release or any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-' for c in release):
        raise ValueError('Invalid release tag')
    if len(repository.split('/')) != 2 or any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-/' for c in repository):
        raise ValueError('Invalid GitHub repository')
    legacy = lfs_records(root)
    manifest_path = root / MANIFEST
    previous = json.loads(manifest_path.read_text())['files'] if manifest_path.is_file() else []
    records = {r['path']: r for r in previous + legacy if not r['path'].startswith('prebuilt/')}
    objects = {}
    for name, record in sorted(records.items()):
        source = safe_target(root, name)
        if not source.is_file() or source.stat().st_size != record['bytes'] or digest(source) != record['sha256']:
            raise ValueError('Missing/modified publisher resource: ' + name)
        if record['bytes'] > maximum:
            raise ValueError('Resource exceeds the chosen bundle size: ' + name)
        objects.setdefault(record['sha256'], (source, record['bytes']))
    groups, group, size = [], [], 0
    for oid, (source, length) in sorted(objects.items()):
        if group and size + length > maximum:
            groups.append(group)
            group, size = [], 0
        group.append(oid)
        size += length
    if group:
        groups.append(group)
    output.mkdir(parents=True, exist_ok=True)
    bundles = []
    for index, group in enumerate(groups, 1):
        name = 'agribot-resources-' + str(index).zfill(2) + '.tar.zst'
        archive = output / name
        temporary = archive.with_suffix(archive.suffix + '.partial')
        print('Packing ' + name + ': ' + str(len(group)) + ' unique objects', flush=True)
        process = subprocess.Popen(['zstd', '-q', '-f', '-T4', '-10', '-o', str(temporary)], stdin=subprocess.PIPE)
        try:
            with tarfile.open(fileobj=process.stdin, mode='w|', format=tarfile.USTAR_FORMAT) as package_file:
                for oid in group:
                    source, length = objects[oid]
                    member = tarfile.TarInfo('objects/' + oid)
                    member.size, member.mode, member.mtime = length, 0o644, 0
                    with source.open('rb') as stream:
                        package_file.addfile(member, stream)
            process.stdin.close()
            if process.wait() != 0 or temporary.stat().st_size >= 2 * 1024**3:
                raise ValueError('Bundle compression failed or exceeds the GitHub release limit')
            os.replace(temporary, archive)
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait()
        bundles.append(dict(name=name, bytes=archive.stat().st_size, sha256=digest(archive), objects=group))
    manifest = dict(format='agribot.release-assets', version=1, release=release,
                    base_url='https://github.com/' + repository + '/releases/download/' + release + '/',
                    bundles=bundles, files=sorted(records.values(), key=lambda r: r['path']))
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    # Exact generated ignore entries leave unrelated user files and source types alone.
    marker = '# BEGIN generated release resources\n'
    ignore_path = root / '.gitignore'
    original = ignore_path.read_text().split(marker)[0].rstrip() + '\n\n'
    def escaped(name):
        return '/' + ''.join('\\' + char if char in ' []*?!#' else char for char in name)
    ignore_path.write_text(original + marker + '\n'.join(escaped(name) for name in sorted(records)) +
                           '\n/prebuilt/*.tar.zst\n# END generated release resources\n')
    (output / 'untrack-paths.nul').write_bytes(b'\0'.join(r['path'].encode() for r in legacy) + b'\0')
    print(json.dumps(dict(files=len(records), objects=len(objects), bundles=len(bundles),
                         download_bytes=sum(r['bytes'] for r in bundles)), indent=2))
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--release', required=True)
    parser.add_argument('--repository', default='Guanzhen-C/agribot_report')
    args = parser.parse_args()
    package(args.root, args.output, args.release, args.repository)


if __name__ == '__main__':
    main()
