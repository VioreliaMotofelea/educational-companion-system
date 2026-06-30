#!/usr/bin/env python3
"""
Run OULAD pipeline end-to-end:
  1) preprocess raw CSV
  2) validate processed JSON
  3) seed/import to backend (optional)
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path


ROOT_DIR = Path(__file__).resolve().parents[2]
SCRIPTS_DIR = ROOT_DIR / "scripts" / "datasets"
DEFAULT_RAW_DIR = ROOT_DIR / "datasets" / "oulad" / "raw"
DEFAULT_PROCESSED_DIR = ROOT_DIR / "datasets" / "oulad" / "processed"


def run_cmd(args: list[str]) -> None:
    print(f"\n$ {' '.join(args)}")
    subprocess.run(args, check=True)


def main() -> None:
    parser = argparse.ArgumentParser(description="Run full OULAD preprocessing + validation + seed pipeline.")
    parser.add_argument("--raw-dir", type=Path, default=DEFAULT_RAW_DIR)
    parser.add_argument("--processed-dir", type=Path, default=DEFAULT_PROCESSED_DIR)
    parser.add_argument("--chunksize", type=int, default=500_000)
    parser.add_argument("--min-clicks-for-completed", type=int, default=5)
    parser.add_argument("--test-user-percent", type=int, default=20)
    parser.add_argument("--modules", nargs="+", default=None)
    parser.add_argument("--max-users", type=int, default=0)
    parser.add_argument("--strict-validate", action="store_true")
    parser.add_argument("--seed-mode", choices=["none", "api", "sql"], default="none")
    parser.add_argument("--backend-url", default="http://localhost:5235")
    parser.add_argument("--include-test", action="store_true")
    parser.add_argument("--batch-size", type=int, default=500)
    parser.add_argument("--timeout-s", type=int, default=30)
    parser.add_argument("--sql-output", type=Path, default=DEFAULT_PROCESSED_DIR / "seed_oulad.sql")
    parser.add_argument("--apply-sql", action="store_true")
    parser.add_argument("--db-url", default="")
    args = parser.parse_args()

    py = sys.executable

    preprocess_cmd = [
        py,
        str(SCRIPTS_DIR / "preprocess_oulad.py"),
        "--raw-dir",
        str(args.raw_dir),
        "--processed-dir",
        str(args.processed_dir),
        "--chunksize",
        str(args.chunksize),
        "--min-clicks-for-completed",
        str(args.min_clicks_for_completed),
        "--test-user-percent",
        str(args.test_user_percent),
        "--max-users",
        str(args.max_users),
    ]
    if args.modules:
        preprocess_cmd.extend(["--modules", *args.modules])
    run_cmd(preprocess_cmd)

    validate_cmd = [
        py,
        str(SCRIPTS_DIR / "validate_processed.py"),
        "--processed-dir",
        str(args.processed_dir),
    ]
    if args.strict_validate:
        validate_cmd.append("--strict")
    run_cmd(validate_cmd)

    if args.seed_mode == "none":
        print("\nPipeline completed (preprocess + validate).")
        return

    seed_cmd = [
        py,
        str(SCRIPTS_DIR / "seed_oulad.py"),
        "--processed-dir",
        str(args.processed_dir),
        "--mode",
        args.seed_mode,
        "--backend-url",
        args.backend_url,
        "--batch-size",
        str(args.batch_size),
        "--timeout-s",
        str(args.timeout_s),
        "--sql-output",
        str(args.sql_output),
    ]
    if args.include_test:
        seed_cmd.append("--include-test")
    if args.apply_sql:
        seed_cmd.append("--apply-sql")
    if args.db_url.strip():
        seed_cmd.extend(["--db-url", args.db_url.strip()])
    run_cmd(seed_cmd)
    print("\nPipeline completed (preprocess + validate + seed).")


if __name__ == "__main__":
    main()
