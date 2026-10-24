#!/usr/bin/env python3
"""Recover this public repository's LFS objects from GitHub's media endpoint.

Downloads are pinned to HEAD and checked against Git's LFS SHA256 identifiers.
No model is copied from a developer's original worktree or private resource set.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
from pathlib import Path
import re
import subprocess
import tempfile
import time
from urllib.parse import quote
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[1]


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args], text=True).strip()


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024**2), b''):
            result.update(block)
    return result.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workers', type=int, choices=range(1, 9), default=4)
    args = parser.parse_args()
    revision = git('rev-parse', 'HEAD')
    cache = Path(git('rev-parse', '--git-dir'))
    if not cache.is_absolute():
        cache = ROOT / cache
    cache /= 'lfs/objects'
    objects = {}
    for row in git('lfs', 'ls-files', '--long').splitlines():
        oid, _, path = row.split(' ', 2)
        if not re.fullmatch('[0-9a-f]{64}', oid) or '\n' in path:
            raise ValueError('Unexpected LFS listing')
        objects.setdefault(oid, path)

    def fetch(item):
        oid, path = item
        target = cache / oid[:2] / oid[2:4] / oid
        if target.exists() and digest(target) == oid:
            return 'cached'
        target.parent.mkdir(parents=True, exist_ok=True)
        url = ('https://media.githubusercontent.com/media/Guanzhen-C/agribot_report/'
               + revision + '/' + quote(path, safe='/'))
        for attempt in range(3):
            temporary = None
            try:
                with tempfile.NamedTemporaryFile(dir=target.parent, delete=False) as output:
                    temporary = Path(output.name)
                    request = Request(url + '?download=1&attempt=' + str(attempt),
                                      headers={'Accept-Encoding': 'identity'})
                    with urlopen(request, timeout=60) as response:
                        while block := response.read(1024**2):
                            output.write(block)
                if digest(temporary) != oid:
                    raise ValueError('SHA256 mismatch: ' + path)
                temporary.replace(target)
                return 'downloaded'
            except Exception:
                if temporary is not None:
                    temporary.unlink(missing_ok=True)
                if attempt == 2:
                    raise
                time.sleep(2 * (attempt + 1))

    with ThreadPoolExecutor(max_workers=args.workers) as executor:
        for count, status in enumerate(executor.map(fetch, objects.items()), 1):
            print(f'{count}/{len(objects)} {status}', flush=True)
    subprocess.run(['git', '-C', str(ROOT), 'lfs', 'checkout'], check=True)


if __name__ == '__main__':
    main()
