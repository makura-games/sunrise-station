#!/usr/bin/env python3

import argparse
import subprocess
import sys
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description="Run one integration-test shard locally.")
    parser.add_argument("shard", type=int, choices=range(8))
    args = parser.parse_args()

    project_root = Path(__file__).resolve().parents[4]
    settings = project_root / ".integration-filters" / f"shard_{args.shard}.runsettings"
    results_dir = project_root / "TestResults" / f"rider-shard-{args.shard}"
    results_dir.mkdir(parents=True, exist_ok=True)

    command = [
        "dotnet",
        "test",
        str(project_root / "bin" / "Content.IntegrationTests" / "Content.IntegrationTests.dll"),
        "--settings",
        str(settings),
        "--logger",
        "trx;LogFileName=results.trx",
        "--logger",
        "console;verbosity=normal",
        "--results-directory",
        str(results_dir),
        "--blame-hang",
        "--blame-hang-timeout",
        "6min",
        "--blame-hang-dump-type",
        "mini",
        "--",
        "NUnit.ConsoleOut=0",
        f"NUnit.WorkDirectory={results_dir}",
    ]

    return subprocess.call(command, cwd=project_root)


if __name__ == "__main__":
    sys.exit(main())
