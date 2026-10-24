#!/usr/bin/env python3
"""Save the user's own AI credential privately, never in tracked source files."""
import argparse
import getpass
import json
import os
from pathlib import Path


def main():
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--path', type=Path, default=root / '.local/llm.json')
    parser.add_argument('--model', default='deepseek-flash')
    args = parser.parse_args()
    key = getpass.getpass('DeepSeek API Key（不回显，不写入Git）: ').strip()
    if not key or any(character.isspace() for character in key):
        raise SystemExit('API Key不能为空或含空白。')
    args.path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    descriptor = os.open(args.path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(descriptor, 'w') as stream:
        json.dump(dict(api_key=key, base_url='https://api.deepseek.com', model=args.model), stream, indent=2)
        stream.write('\n')
    args.path.chmod(0o600)
    print('已保存私人配置：' + str(args.path) + '；未打印密钥。')


if __name__ == '__main__':
    main()
