"""Identify the runtime source/assets independently of documentation-only commits."""
import hashlib
import json
import argparse
import os
from pathlib import Path
import subprocess

ROOTS = ('unity', 'ros2_ws/src', 'ros2_ws/build_ros2_humble.sh', 'scripts', 'tools', 'resources',
         '.gitattributes', '.gitignore')


def fingerprint(root, require_clean=True):
    root = Path(root)
    def git(*args):
        return subprocess.check_output(['git', '-C', str(root), *args])
    dirty = git('diff', '--name-only', '-z', '--', *ROOTS).decode().strip('\0').split('\0')
    staged = git('diff', '--cached', '--name-only', '-z', '--', *ROOTS).decode().strip('\0').split('\0')
    untracked = git('ls-files', '--others', '--exclude-standard', '-z', '--', *ROOTS).decode().strip('\0').split('\0')
    changes = set(dirty + staged + untracked) - {''}
    if require_clean:
        if changes:
            raise ValueError('运行源码或资源有未提交修改，请先完成构建版本提交；不会把旧程序当作新版本。')
    records = []
    for value in git('ls-files', '-s', '-z', '--', *ROOTS).split(b'\0'):
        if not value:
            continue
        identity, path = value.split(b'\t', 1)
        mode, blob, stage = identity.decode().split()
        if stage != '0':
            raise ValueError('运行输入存在Git冲突')
        name = path.decode()
        if require_clean or name not in changes:
            records.append(dict(path=name, mode=mode, git_blob=blob))
    if not require_clean:
        for name in sorted(changes):
            file = root / name
            if not file.exists() and not file.is_symlink():
                continue
            sha = hashlib.sha256()
            if file.is_symlink():
                mode = '120000'
                sha.update(os.readlink(file).encode())
            else:
                mode = '100755' if file.stat().st_mode & 0o111 else '100644'
                with file.open('rb') as stream:
                    for block in iter(lambda: stream.read(4 * 1024**2), b''):
                        sha.update(block)
            records.append(dict(path=name, mode=mode, working_sha256=sha.hexdigest()))
        records.sort(key=lambda record: record['path'])
    encoded = json.dumps(records, ensure_ascii=False, separators=(',', ':')).encode()
    result = dict(method='git-runtime-input-blobs-v1' if require_clean else 'git-runtime-working-tree-v1', sha256=hashlib.sha256(encoded).hexdigest(),
                files=len(records), source_commit=git('rev-parse', 'HEAD').decode().strip())
    if not require_clean:
        result['uncommitted_runtime_changes'] = bool(changes)
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--working-tree', action='store_true', help='Explicit development build; hash current edited and new files instead of claiming a clean release')
    args = parser.parse_args()
    print(json.dumps(fingerprint(Path(__file__).resolve().parents[1], require_clean=not args.working_tree), indent=2))
