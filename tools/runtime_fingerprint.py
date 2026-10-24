"""Identify the runtime source/assets independently of documentation-only commits."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOTS = ('unity', 'ros2_ws/src', 'ros2_ws/build_ros2_humble.sh', 'scripts', 'tools', 'resources',
         '.gitattributes', '.gitignore')


def fingerprint(root, require_clean=True):
    root = Path(root)
    def git(*args):
        return subprocess.check_output(['git', '-C', str(root), *args])
    if require_clean:
        dirty = git('diff', '--name-only', '--', *ROOTS).decode().splitlines()
        staged = git('diff', '--cached', '--name-only', '--', *ROOTS).decode().splitlines()
        untracked = git('ls-files', '--others', '--exclude-standard', '--', *ROOTS).decode().splitlines()
        if dirty or staged or untracked:
            raise ValueError('运行源码或资源有未提交修改，请先完成构建版本提交；不会把旧程序当作新版本。')
    records = []
    for value in git('ls-files', '-s', '-z', '--', *ROOTS).split(b'\0'):
        if not value:
            continue
        identity, path = value.split(b'\t', 1)
        mode, blob, stage = identity.decode().split()
        if stage != '0':
            raise ValueError('运行输入存在Git冲突')
        records.append(dict(path=path.decode(), mode=mode, git_blob=blob))
    encoded = json.dumps(records, ensure_ascii=False, separators=(',', ':')).encode()
    return dict(method='git-runtime-input-blobs-v1', sha256=hashlib.sha256(encoded).hexdigest(),
                files=len(records), source_commit=git('rev-parse', 'HEAD').decode().strip())


if __name__ == '__main__':
    print(json.dumps(fingerprint(Path(__file__).resolve().parents[1]), indent=2))
