import json
import os
import re
import subprocess
import sys
from pathlib import Path


def load_workflow_paths(workflow: Path) -> list[str]:
    lines = workflow.read_text(encoding="utf-8-sig").splitlines()
    in_push = False
    in_paths = False
    paths = []

    for line in lines:
        if line == "  push:":
            in_push = True
            continue
        if in_push and line.startswith("  ") and not line.startswith("    "):
            break
        if in_push and line == "    paths:":
            in_paths = True
            continue
        if not in_paths or not line.startswith("      - "):
            continue

        value = line.removeprefix("      - ").strip()
        paths.append(value[1:-1] if value[:1] in {"'", '"'} else value)

    if not paths:
        raise RuntimeError(f"Не найдены пути on.push.paths в {workflow}")
    return paths


def matches(path: str, pattern: str) -> bool:
    expression = re.escape(pattern).replace(r"\*\*", ".*").replace(r"\*", "[^/]*").replace(r"\?", "[^/]")
    return re.fullmatch(expression, path) is not None


def packaging_needed(changed_paths: list[str], patterns: list[str]) -> bool:
    for path in changed_paths:
        included = False
        for pattern in patterns:
            excluded = pattern.startswith("!")
            if matches(path, pattern.removeprefix("!")):
                included = not excluded
        if included:
            return True
    return False


def pull_request_paths() -> list[str]:
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text(encoding="utf-8"))
    pull_request = event["pull_request"]["number"]
    result = subprocess.run(
        [
            "gh",
            "api",
            "--paginate",
            f"repos/{os.environ['GITHUB_REPOSITORY']}/pulls/{pull_request}/files",
            "--jq",
            ".[] | .filename, (.previous_filename // empty)",
        ],
        check=True,
        stdout=subprocess.PIPE,
        text=True,
    )
    return result.stdout.splitlines()


def main() -> None:
    patterns = load_workflow_paths(Path(sys.argv[1]))
    needed = os.environ.get("GITHUB_EVENT_NAME") != "pull_request" or packaging_needed(
        pull_request_paths(), patterns
    )
    print(f"packaging={str(needed).lower()}")


if __name__ == "__main__":
    main()
