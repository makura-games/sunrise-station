#!/usr/bin/env python3

import sys
from pathlib import Path

PRIVATE_PROJECTS = ("Content.Server", "Content.Shared", "Content.Client")


def main() -> None:
    # Проекты подключают приватный C# код из SunrisePrivate относительно корня исходников.
    for project in PRIVATE_PROJECTS:
        private_dir = Path("SunrisePrivate") / project
        if not private_dir.is_dir() or not any(
            source.is_file() for source in private_dir.rglob("*.cs")
        ):
            print(
                f"::error::Private C# sources are missing in {private_dir}; "
                "refusing to publish without private code.",
                file=sys.stderr,
            )
            raise SystemExit(1)


if __name__ == "__main__":
    main()
