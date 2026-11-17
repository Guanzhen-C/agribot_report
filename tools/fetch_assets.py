#!/usr/bin/env python3
"""Restore the resource version pinned by this checkout, without Git LFS."""
import argparse
import json
from pathlib import Path

from release_assets import inspect, restore


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--bundle-dir', type=Path, help='Use previously downloaded release bundles offline')
    parser.add_argument('--repair', action='store_true', help='Explicitly replace locally edited managed assets')
    parser.add_argument('--check', action='store_true', help='Read-only check; no download')
    parser.add_argument('--hashes', action='store_true', help='Check all resource SHA-256 values')
    args = parser.parse_args()
    if args.check:
        issues = inspect(args.root, hashes=args.hashes)
        print(json.dumps(dict(ok=not issues, issues=issues), ensure_ascii=False, indent=2))
        return int(bool(issues))
    print(json.dumps(restore(args.root, args.bundle_dir, args.repair), indent=2))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
