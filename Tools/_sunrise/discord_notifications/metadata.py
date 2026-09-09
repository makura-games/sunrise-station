"""Дополнительные данные карточек MoMMI: реакции и проверки PR."""

import re
from collections import Counter

from .github import GitHubError


def enrich_event(event: str, payload: dict, github, config: dict, log) -> None:
    if event not in {"pull_request_target", "pull_request", "issues"}:
        return
    subject = payload.get("pull_request") or payload.get("issue") or {}
    number = subject.get("number")
    if type(number) is not int or number <= 0:
        return
    if config["display"]["show_reactions"]:
        try:
            reactions = github.json(
                "GET",
                f"{github.root}/issues/{number}/reactions",
                params={"per_page": 100},
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
        checks = github.json("GET", f"{github.root}/commits/{head}/check-runs")
        payload["_discord_checks"] = checks["check_runs"]
    except GitHubError as error:
        log(f"Не удалось обновить проверки PR #{number}: {error}.")
