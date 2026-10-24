#!/usr/bin/env python3
"""Package a verified Linux build for clone-and-run users without Unity Editor."""
import argparse
import json
from pathlib import Path
import subprocess

from delivery_assets import digest
from runtime_fingerprint import fingerprint

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', type=Path, default=ROOT / 'dist/unity')
    args = parser.parse_args()
    inputs = fingerprint(ROOT)
    for name in ('AgribotConfigurator.x86_64', 'libagribot_linux_ime.so', 'start_agribot_configurator.sh'):
        if not (args.app / name).is_file():
            raise SystemExit('缺少已构建文件：' + name)
    if args.app.name != 'unity':
        raise SystemExit('构建目录必须命名为unity，以保留相对目录结构。')
    proof = args.app / 'build-info.json'
    if not proof.is_file() or json.loads(proof.read_text()).get('sha256') != inputs['sha256']:
        raise SystemExit('缺少匹配的构建时指纹。请先用scripts/build_unity.sh从当前干净版本重建，不能给旧程序补写版本信息。')
    output = ROOT / 'prebuilt'
    output.mkdir(exist_ok=True)
    archive = output / 'agribot-configurator-linux-x86_64.tar.zst'
    subprocess.run(['tar', '-I', 'zstd -T4 -10', '-cf', str(archive), '-C', str(args.app.parent), 'unity'], check=True)
    manifest = dict(format='agribot.prebuilt-app', version=1, platform='ubuntu-22.04-x86_64',
                    archive=archive.name, sha256=digest(archive), bytes=archive.stat().st_size, runtime_inputs=inputs)
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    main()
