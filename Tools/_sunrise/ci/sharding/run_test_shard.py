#!/usr/bin/env python3

import argparse
import subprocess
import sys
from pathlib import Path

from failure_report import report_failures


def run_and_capture(command, project_root: Path, console_log: Path) -> int:
    """Запускает шард, сохраняя полный консольный лог и код завершения."""

    with console_log.open("w", encoding="utf-8", newline="") as log:
        process = subprocess.Popen(
            command,
            cwd=project_root,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
            bufsize=1,
        )

        assert process.stdout is not None
        try:
            for line in process.stdout:
                sys.stdout.write(line)
                sys.stdout.flush()
                log.write(line)
                log.flush()
        except KeyboardInterrupt:
            process.terminate()
            process.wait()
            raise
        finally:
            process.stdout.close()

        return process.wait()


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

    status = run_and_capture(command, project_root, results_dir / "console.log")
    # При MapWarningTo=Failed после ошибки GameTest в TRX может остаться только
    # предупреждение teardown.
    # Сохранённый поток консоли содержит исходное утверждение.
    if status != 0:
        try:
            report_failures(results_dir / "console.log")
        except Exception as error:  # pragma: no cover - не меняем результат тестов из-за отчёта
            print(f"Unable to report shard failures: {error}", file=sys.stderr)
    return status


if __name__ == "__main__":
    sys.exit(main())
