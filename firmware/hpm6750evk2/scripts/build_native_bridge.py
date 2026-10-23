#!/usr/bin/env python3
"""Build real flash-XIP bridge images; never flash a connected device."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PROFILES = {"ackermann": (18, 0x202, 1000000), "tracked": (1, 0x101, 250000)}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", choices=PROFILES, default="ackermann")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    machine, protocol, bitrate = PROFILES[args.profile]
    build = ROOT / "build" / args.profile
    output = args.output or ROOT / "build" / "releases" / args.profile
    subprocess.run(["cmake", "-S", str(ROOT / "firmware/vcu_configurator"), "-B", str(build),
        "-GNinja", "-DBOARD=hpm6750evk2", "-DCMAKE_BUILD_TYPE=Release", "-DHPM_BUILD_TYPE=flash_xip",
        "-DVCU_RUNTIME_ENABLE=ON", f"-DVCU_BRIDGE_MACHINE_ID={machine}"], check=True)
    subprocess.run(["cmake", "--build", str(build), "--parallel", str(min(os.cpu_count() or 2, 8))], check=True)
    output.mkdir(parents=True, exist_ok=True)
    files = {}
    for ext in ("bin", "elf"):
        source = build / "output" / f"demo.{ext}"
        if not source.is_file():
            raise RuntimeError(f"Missing compiler artifact: {source}")
        target = output / f"hpm6750_{args.profile}.{ext}"
        shutil.copy2(source, target)
        files[target.name] = {"sha256": hashlib.sha256(target.read_bytes()).hexdigest(), "size": target.stat().st_size}
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    dirty = bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).strip())
    source_files = [ROOT / "firmware/vcu_configurator/CMakeLists.txt",
        *sorted((ROOT / "firmware/vcu_configurator/src").glob("*.[ch]"))]
    source_sha256 = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in source_files}
    manifest = {"format": "agribot.hpm6750-firmware", "version": 1, "firmware_version": "1.0.0",
        "board": "hpm6750evk2", "profile": args.profile, "machine_id": machine, "protocol_id": protocol,
        "transport": "classic_can", "bitrate": bitrate, "build_type": "flash_xip",
        "source_revision": revision, "source_dirty": dirty, "source_sha256": source_sha256, "files": files,
        "flashing": "manual", "board_verified": False}
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(output)

if __name__ == "__main__":
    main()
