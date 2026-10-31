#!/usr/bin/env python3

import os
import subprocess
import sys
from pathlib import Path


SHARD_COUNT = 8


def main():
    project_root = Path(__file__).resolve().parents[4]
    filter_dir = project_root / ".integration-filters"
    environment = os.environ.copy()

    dotnet_home = Path.home() / ".dotnet"
    environment["PATH"] = str(dotnet_home) + os.pathsep + environment.get("PATH", "")
    environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US"

    print("Building Content.IntegrationTests...", file=sys.stderr)
    subprocess.run(
        [
            "dotnet",
            "build",
            "Content.IntegrationTests/Content.IntegrationTests.csproj",
            "--configuration",
            "DebugOpt",
            "/m:1",
            "/nodeReuse:false",
        ],
        cwd=project_root,
        env=environment,
        check=True,
    )

    print("Discovering integration tests...", file=sys.stderr)
    discovered = subprocess.run(
        [
            "dotnet",
            "test",
            "--list-tests",
            "--no-build",
            "--no-restore",
            "--configuration",
            "DebugOpt",
            "Content.IntegrationTests/Content.IntegrationTests.csproj",
            "--",
            "NUnit.DisplayName=FullName",
        ],
        cwd=project_root,
        env=environment,
        stdout=subprocess.PIPE,
        text=True,
        check=True,
    )

    print("Generating integration test runsettings...", file=sys.stderr)
    subprocess.run(
        [
            sys.executable,
            str(Path(__file__).with_name("test_shard_filter.py")),
            "generate",
            str(SHARD_COUNT),
            str(filter_dir),
        ],
        cwd=project_root,
        input=discovered.stdout,
        text=True,
        check=True,
    )


if __name__ == "__main__":
    main()
