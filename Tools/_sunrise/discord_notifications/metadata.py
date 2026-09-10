"""Связи коммитов, реакции и статусы PR для карточек GitHub."""

import re
from collections import Counter

from .github import GitHubError


def enrich_event(event: str, payload: dict, github, config: dict, log) -> None:
    if event == "delete" and payload.get("ref_type") == "branch":
        branch = str(payload.get("ref") or "")
        if not branch:
            raise ValueError("В событии удаления отсутствует имя ветки")
        owner = github.repository.split("/", 1)[0]
        try:
            pull = next(
                iter(
                    github.pages(
                        f"{github.root}/pulls",
                        state="all",
                        head=f"{owner}:{branch}",
                    )
                ),
                None,
            )
        except GitHubError as error:
            raise GitHubError(
                f"Не удалось проверить связь удалённой ветки {branch} с PR: "
                f"{error}. Проверка будет повторена"
            ) from error
        if pull:
            payload["_discord_related_pr"] = pull["number"]
            log(
                f"Удаление ветки {branch} пропущено: "
                f"она связана с PR #{pull['number']}."
            )
        return
    if event == "push":
        commits = []
        for commit in payload.get("commits", []):
            sha = commit.get("id", "")
            if not re.fullmatch(r"[0-9a-fA-F]{40}", sha):
                raise ValueError("Некорректный идентификатор коммита")
            try:
                pulls = github.pages(f"{github.root}/commits/{sha}/pulls")
                merged = next(
                    (
                        pull
                        for pull in pulls
                        if pull["merged_at"]
                        and pull["base"]["ref"] == "master"
                        and pull["base"]["repo"]["full_name"].casefold()
                        == github.repository.casefold()
                    ),
                    None,
                )
            except GitHubError as error:
                raise GitHubError(
                    f"Не удалось проверить связь коммита {sha[:7]} с PR: "
                    f"{error}. Проверка будет повторена"
                ) from error
            if merged:
                log(
                    f"Коммит {sha[:7]} пропущен: вошёл в master "
                    f"через PR #{merged['number']}."
                )
            else:
                commits.append(commit)
        payload["commits"] = commits
        return
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
    if event == "pull_request_review_comment":
        comment = payload.get("comment", {})
        payload.pop("_discord_review_state", None)
        if comment.get("in_reply_to_id") is not None:
            return
        review_id = comment.get("pull_request_review_id")
        if type(review_id) is int and review_id > 0:
            try:
                review = github.json(
                    "GET", f"{github.root}/pulls/{number}/reviews/{review_id}"
                )
                payload["_discord_review_state"] = str(review["state"]).lower()
            except GitHubError as error:
                log(f"Статус ревью #{review_id} недоступен: {error}.")
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
