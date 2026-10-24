#!/usr/bin/env python3
"""Verify and unpack the matching offline app without consulting developer paths."""
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import tarfile
import tempfile

from delivery_assets import digest
from runtime_fingerprint import fingerprint

ROOT = Path(__file__).resolve().parents[1]


def main():
    metadata = json.loads((ROOT / 'prebuilt/manifest.json').read_text())
    archive = ROOT / 'prebuilt' / metadata['archive']
    if Path(metadata['archive']).name != metadata['archive']:
        raise SystemExit('程序包文件名无效')
    current = fingerprint(ROOT)
    if current['sha256'] != metadata['runtime_inputs']['sha256']:
        raise SystemExit('预编译程序与当前运行源码不一致，请使用匹配版本或从源码重新构建。')
    if archive.stat().st_size != metadata['bytes'] or digest(archive) != metadata['sha256']:
        raise SystemExit('程序包不完整，请执行git lfs pull。')
    destination = ROOT / 'dist'
    destination.mkdir(exist_ok=True)
    if (destination / 'unity').exists():
        raise SystemExit('dist/unity已存在。请先退出应用并把旧目录移到备份位置，避免覆盖正在运行的程序。')
    with tempfile.TemporaryDirectory(prefix='.app-', dir=destination) as temporary:
        staging = Path(temporary)
        process = subprocess.Popen(['zstd', '-dc', str(archive)], stdout=subprocess.PIPE)
        try:
            with tarfile.open(fileobj=process.stdout, mode='r|') as package:
                for member in package:
                    relative = PurePosixPath(member.name)
                    if relative.is_absolute() or '..' in relative.parts or not relative.parts or relative.parts[0] != 'unity':
                        raise ValueError('程序包包含不安全路径')
                    if not member.isfile() and not member.isdir():
                        raise ValueError('程序包只允许普通文件与目录')
                    target = staging / member.name
                    if member.isdir():
                        target.mkdir(parents=True, exist_ok=True)
                        continue
                    target.parent.mkdir(parents=True, exist_ok=True)
                    with package.extractfile(member) as source, target.open('wb') as output:
                        while data := source.read(4 * 1024**2):
                            output.write(data)
                    target.chmod(member.mode & 0o755)
            if process.wait() != 0:
                raise RuntimeError('程序包解压失败')
            info = json.loads((staging / 'unity/build-info.json').read_text())
            if info['sha256'] != current['sha256']:
                raise ValueError('解压后的程序版本不符')
            os.replace(staging / 'unity', destination / 'unity')
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait()
    print('程序已安装：' + str(destination / 'unity/start_agribot_configurator.sh'))


if __name__ == '__main__':
    main()
