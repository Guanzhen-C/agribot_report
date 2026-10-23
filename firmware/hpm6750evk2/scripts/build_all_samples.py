#!/usr/bin/env python3

import argparse
import concurrent.futures
import json
import os
import subprocess
import time
from pathlib import Path


SKIP_MARKERS = (
    "can not support this sample",
    "target ram:debug has been excluded for this example",
)
TOOLCHAIN_SKIP_MARKERS = ("cannot find -ldsp",)


def parse_args():
    parser = argparse.ArgumentParser(
        description="Build every HPM SDK sample compatible with hpm6750evk2."
    )
    parser.add_argument("--workers", type=int, default=6)
    parser.add_argument("--build-jobs", type=int, default=2)
    parser.add_argument("--timeout", type=int, default=600)
    parser.add_argument(
        "--retry-failed",
        action="store_true",
        help="Retry only failures from the previous summary and preserve other results.",
    )
    return parser.parse_args()


def find_samples(sample_root):
    samples = []
    for cmake_file in sample_root.rglob("CMakeLists.txt"):
        if "find_package(hpm-sdk" in cmake_file.read_text(errors="ignore"):
            samples.append(cmake_file.parent)
    return sorted(samples)


def run_command(command, log_file, timeout):
    with log_file.open("ab") as stream:
        stream.write(("\n$ " + " ".join(command) + "\n").encode())
        stream.flush()
        try:
            result = subprocess.run(
                command,
                stdout=stream,
                stderr=subprocess.STDOUT,
                timeout=timeout,
                check=False,
            )
            return result.returncode, None
        except subprocess.TimeoutExpired:
            stream.write(f"\nTIMEOUT after {timeout} seconds\n".encode())
            return 124, "timeout"


def build_sample(sample_dir, sample_root, build_root, log_root, args):
    relative = sample_dir.relative_to(sample_root)
    build_dir = build_root / relative
    log_file = log_root / relative.with_suffix(".log")
    log_file.parent.mkdir(parents=True, exist_ok=True)
    build_dir.mkdir(parents=True, exist_ok=True)
    log_file.write_bytes(b"")
    started = time.monotonic()

    configure = [
        "cmake",
        "-S",
        str(sample_dir),
        "-B",
        str(build_dir),
        "-GNinja",
        "-DBOARD=hpm6750evk2",
        "-DCMAKE_BUILD_TYPE=debug",
        "-DHPM_BUILD_TYPE=ram",
        "-DCMAKE_POLICY_VERSION_MINIMUM=3.5",
    ]
    configure_rc, error = run_command(configure, log_file, args.timeout)
    if configure_rc != 0:
        output = log_file.read_text(errors="ignore").lower()
        status = "skipped" if any(marker in output for marker in SKIP_MARKERS) else "failed"
        return {
            "sample": relative.as_posix(),
            "status": status,
            "phase": "configure",
            "returncode": configure_rc,
            "error": error,
            "seconds": round(time.monotonic() - started, 3),
            "log": str(log_file),
        }

    build = ["cmake", "--build", str(build_dir), "--parallel", str(args.build_jobs)]
    build_rc, error = run_command(build, log_file, args.timeout)
    output = log_file.read_text(errors="ignore").lower()
    if build_rc == 0:
        status = "passed"
    elif any(marker in output for marker in TOOLCHAIN_SKIP_MARKERS):
        status = "skipped"
    else:
        status = "failed"
    return {
        "sample": relative.as_posix(),
        "status": status,
        "phase": "build",
        "returncode": build_rc,
        "error": error,
        "seconds": round(time.monotonic() - started, 3),
        "log": str(log_file),
    }


def write_reports(results, report_root, total, elapsed):
    ordered = sorted(results, key=lambda item: item["sample"])
    counts = {
        status: sum(item["status"] == status for item in ordered)
        for status in ("passed", "skipped", "failed")
    }
    report = {
        "board": "hpm6750evk2",
        "configuration": "ram_debug",
        "total": total,
        "completed": len(ordered),
        "counts": counts,
        "elapsed_seconds": round(elapsed, 3),
        "results": ordered,
    }
    (report_root / "summary.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    )
    with (report_root / "summary.tsv").open("w") as stream:
        stream.write("status\tphase\tseconds\tsample\tlog\n")
        for item in ordered:
            stream.write(
                f'{item["status"]}\t{item["phase"]}\t{item["seconds"]}\t'
                f'{item["sample"]}\t{item["log"]}\n'
            )
    return counts


def main():
    args = parse_args()
    workspace = Path(__file__).resolve().parents[1]
    sdk_base = Path(os.environ.get("HPM_SDK_BASE", workspace / ".hpm/hpm_sdk"))
    sample_root = sdk_base / "samples"
    build_root = workspace / "build/all-samples"
    report_root = workspace / "build/test-results/all-samples"
    log_root = report_root / "logs"
    report_root.mkdir(parents=True, exist_ok=True)

    samples = find_samples(sample_root)
    total = len(samples)
    results = []
    selected_samples = samples
    previous_report = report_root / "summary.json"
    if args.retry_failed and previous_report.exists():
        previous_results = json.loads(previous_report.read_text())["results"]
        failed = {
            item["sample"] for item in previous_results if item["status"] == "failed"
        }
        results = [
            item for item in previous_results if item["status"] != "failed"
        ]
        selected_samples = [
            sample
            for sample in samples
            if sample.relative_to(sample_root).as_posix() in failed
        ]
    started = time.monotonic()
    print(
        f"Building {len(selected_samples)} of {total} samples with "
        f"{args.workers} workers"
    )

    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as executor:
        pending = {
            executor.submit(
                build_sample, sample, sample_root, build_root, log_root, args
            ): sample
            for sample in selected_samples
        }
        for future in concurrent.futures.as_completed(pending):
            result = future.result()
            results.append(result)
            counts = write_reports(
                results, report_root, total, time.monotonic() - started
            )
            print(
                f'[{len(results):3}/{total}] {result["status"].upper():7} '
                f'{result["sample"]} '
                f'(pass={counts["passed"]} skip={counts["skipped"]} '
                f'fail={counts["failed"]})',
                flush=True,
            )

    counts = write_reports(results, report_root, total, time.monotonic() - started)
    print(json.dumps(counts, sort_keys=True))
    return 1 if counts["failed"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
