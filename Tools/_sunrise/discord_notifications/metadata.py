"""Реакции и статусы PR для карточек GitHub."""

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
    if event == "pull_request_review_comment":
        review_id = payload.get("comment", {}).get("pull_request_review_id")
        if type(review_id) is int and review_id > 0:
            try:
                review = github.json(
                    "GET", f"{github.root}/pulls/{number}/reviews/{review_id}"
                )
                payload["_discord_review_state"] = str(review["state"]).lower()
            except GitHubError as error:
                payload.pop("_discord_review_state", None)
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
