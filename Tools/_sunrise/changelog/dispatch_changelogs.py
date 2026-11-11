#!/usr/bin/env python3
import json
import os
import re
from collections.abc import Mapping
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import quote
from urllib.request import Request, urlopen

from changelog_targets import load_target, target_paths


GITHUB_API_URL = os.environ.get("GITHUB_API_URL", "https://api.github.com")
HTTP_REQUEST_TIMEOUT = 30
PUBLISH_WORKFLOW = "sunrise-publish-discord-changelog.yml"
SHA_RE = re.compile(r"^[0-9a-f]{40,64}$")
RUN_ID_RE = re.compile(r"^[1-9][0-9]*$")
PUBLISH_RUN_TITLE_RE = re.compile(r"^Publish Stable (?P<sha>[0-9a-fA-F]{40})$")


def require_environment(name: str) -> str:
    value = os.environ.get(name, "").strip()
    if not value:
        raise RuntimeError(f"Переменная {name} не задана")
    return value


def github_request(url: str, token: str, body: dict | None = None) -> dict:
    data = json.dumps(body).encode("utf-8") if body is not None else None
    request = Request(
        url,
        data=data,
        method="POST" if body is not None else "GET",
        headers={
            "Accept": "application/vnd.github+json",
            "Authorization": f"Bearer {token}",
            "X-GitHub-Api-Version": "2022-11-28",
        },
    )
    try:
        with urlopen(request, timeout=HTTP_REQUEST_TIMEOUT) as response:
            payload = response.read()
            return json.loads(payload) if payload else {}
    except HTTPError as error:
        details = error.read().decode("utf-8", errors="replace")[:500]
        raise RuntimeError(f"GitHub API вернул {error.code}: {details}") from error


def published_run_sha(run: Mapping) -> str | None:
    # При откате head_commit относится к сценарию, а выбранный SHA хранится в заголовке запуска.
    match = PUBLISH_RUN_TITLE_RE.fullmatch(str(run.get("display_title", "")))
    if match:
        return match.group("sha").lower()

    # Старые публикации без выбора коммита используют SHA самого запуска.
    head_commit = run.get("head_commit")
    sha = head_commit.get("id") if isinstance(head_commit, Mapping) else None
    return sha if isinstance(sha, str) else None


def resolve_released_sha(repository: str, token: str, source_run_id: str) -> str:
    if not RUN_ID_RE.fullmatch(source_run_id):
        raise RuntimeError("SOURCE_WORKFLOW_RUN_ID должен содержать числовой ID запуска")

    released_sha = os.environ.get("RELEASED_SHA", "").strip()
    if not released_sha:
        run = github_request(
            f"{GITHUB_API_URL}/repos/{repository}/actions/runs/{quote(source_run_id, safe='')}",
            token,
        )
        released_sha = published_run_sha(run) or ""

    if not SHA_RE.fullmatch(released_sha):
        raise RuntimeError("Не удалось определить SHA опубликованного коммита")
    return released_sha


def dispatch_target(
    repository: str,
    token: str,
    workflow_ref: str,
    source_run_id: str,
    released_sha: str,
    target_id: str,
) -> None:
    workflow = quote(PUBLISH_WORKFLOW, safe="")
    github_request(
        f"{GITHUB_API_URL}/repos/{repository}/actions/workflows/{workflow}/dispatches",
        token,
        {
            "ref": workflow_ref,
            "inputs": {
                "target_id": target_id,
                "released_sha": released_sha,
                "source_workflow_run_id": source_run_id,
            },
        },
    )
    print(f"Запущена публикация цели {target_id} для {released_sha}")


def main() -> None:
    repository = require_environment("GITHUB_REPOSITORY")
    token = require_environment("GITHUB_TOKEN")
    source_run_id = require_environment("SOURCE_WORKFLOW_RUN_ID")
    workflow_ref = require_environment("TARGET_WORKFLOW_REF")
    released_sha = resolve_released_sha(repository, token, source_run_id)
    repo_root = Path(__file__).resolve().parents[3]

    errors: list[str] = []
    for path in target_paths(repo_root):
        try:
            target = load_target(path, repo_root)
            dispatch_target(
                repository,
                token,
                workflow_ref,
                source_run_id,
                released_sha,
                target.target_id,
            )
        except Exception as error:
            message = f"{path.name}: {error}"
            print(f"::error::{message}")
            errors.append(message)

    if errors:
        raise RuntimeError("Не удалось запустить часть целей чейнджлога: " + "; ".join(errors))


if __name__ == "__main__":
    main()
