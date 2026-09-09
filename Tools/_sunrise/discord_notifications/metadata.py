"""Дополнительные данные карточек MoMMI: реакции и проверки PR."""

import re
from collections import Counter

from .github import GitHubError


def enrich_event(event: str, payload: dict, github, config: dict, log) -> None:
    subject = payload.get("pull_request") or payload.get("issue") or {}
    number = subject.get("number")
    if type(number) is not int or number <= 0:
        return
    if payload.get("pull_request") or subject.get("pull_request"):
        try:
            payload["_discord_pr_status"] = github.pull_status(number)
        except GitHubError as error:
            payload.pop("_discord_pr_status", None)
            log(
                f"Статус PR #{number} недоступен: {error}. "
                "Не подменяем его результатом одного ревью."
            )
    if event not in {"pull_request_target", "pull_request", "issues"}:
        return
    if config["display"]["show_reactions"]:
        try:
            reactions = github.pages(
                f"{github.root}/issues/{number}/reactions"
            )
            subject["reactions"] = dict(
                Counter(reaction["content"] for reaction in reactions)
            )
        except GitHubError as error:
            log(f"Не удалось обновить реакции PR/задачи #{number}: {error}.")
    if event == "issues" or not config["display"]["show_checks"]:
        return
    head = subject.get("head", {}).get("sha")
    if not isinstance(head, str) or not re.fullmatch(r"[0-9a-f]{40,64}", head):
        return
    try:
        details = github.json("GET", f"{github.root}/pulls/{number}")
        subject["mergeable"] = details.get("mergeable")
        revisions = [head]
        merge = details.get("merge_commit_sha")
        if (
            details.get("state") == "open"
            and details.get("head", {}).get("sha") == head
            and isinstance(merge, str)
            and re.fullmatch(r"[0-9a-f]{40,64}", merge)
            and merge != head
        ):
            revisions.append(merge)
        checks = {}
        for revision in revisions:
            for check in github.pages(
                f"{github.root}/commits/{revision}/check-runs", "check_runs"
            ):
                key = (check.get("app", {}).get("id"), check["name"])
                if check.get("id", 0) >= checks.get(key, {}).get("id", 0):
                    checks[key] = check
        payload["_discord_checks"] = list(checks.values())
    except GitHubError as error:
        log(f"Не удалось обновить проверки PR #{number}: {error}.")
    try:
        statuses = {}
        for status in github.pages(f"{github.root}/commits/{head}/statuses"):
            statuses.setdefault(status["context"], status)
        payload.setdefault("_discord_checks", []).extend(
            {
                "name": status["context"],
                "status": "completed",
                "conclusion": status["state"],
                "html_url": status["target_url"],
            }
            for status in statuses.values()
        )
    except GitHubError as error:
        log(f"Не удалось обновить внешние статусы PR #{number}: {error}.")
