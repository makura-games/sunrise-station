"""Преобразование недоверенных данных GitHub в небольшую карточку."""

import re
from fnmatch import fnmatchcase
from urllib.parse import urlsplit


def truncate(value: str, limit: int) -> str:
    value = value.strip()
    encoded = value.encode("utf-16-le", "replace")
    if len(encoded) > limit * 2:
        value = encoded[: (limit - 1) * 2].decode("utf-16-le", "ignore") + "…"
    return value or "Без описания"


def text(value: object, limit: int = 500) -> str:
    value = re.sub(r"<!--[\s\S]*?-->", "", str(value or ""))
    value = re.sub(r"[\x00-\x08\x0b-\x1f\x7f]", "", value)
    value = value.replace("@", "＠")
    value = re.sub(r"([\\`*_{}\[\]()<>|~])", r"\\\1", value)
    return truncate(value, limit)


def safe_url(value: object, repository: str) -> str:
    value = str(value or "")
    parsed = urlsplit(value)
    if (
        parsed.scheme == "https"
        and parsed.netloc == "github.com"
        and parsed.path.startswith(f"/{repository}/")
        and not re.search(r"[\s<>\x00-\x1f]", value)
    ):
        return value
    return f"https://github.com/{repository}"


def is_ignored(account: dict, config: dict) -> bool:
    filters = config["filters"]
    return (
        filters["ignore_bots"]
        and account.get("type") == "Bot"
        or str(account.get("login", "")).casefold()
        in {name.casefold() for name in filters["ignored_users"]}
    )


def format_event(
    event: str, payload: dict, config: dict
) -> tuple[dict | None, str]:
    event = "pull_request" if event == "pull_request_target" else event
    action = payload.get("action", event)
    accepted = config["events"].get(event, [])
    if "*" not in accepted and action not in accepted:
        return None, f"Событие {event}/{action} выключено в конфигурации"
    repository = payload["repository"]["full_name"]
    pull = payload.get("pull_request") or {}
    issue = payload.get("issue") or {}
    subject = (
        pull
        or issue
        or payload.get("discussion")
        or payload.get("release")
        or {}
    )
    comment = payload.get("comment") or {}
    review = payload.get("review") or {}
    sender = payload.get("sender") or {}
    actor = comment.get("user") or review.get("user") or sender
    merged = (
        event == "pull_request" and action == "closed" and pull.get("merged")
    )
    if is_ignored(actor, config) and not (
        merged and not is_ignored(pull.get("user") or {}, config)
    ):
        return (
            None,
            f"Служебная активность {text(actor.get('login'), 100)} исключена",
        )
    labels = {item["name"].casefold() for item in subject.get("labels", [])}
    if labels & {
        name.casefold() for name in config["filters"]["ignored_labels"]
    }:
        return None, "Метка объекта исключена конфигурацией"
    branch = pull.get("base", {}).get("ref", "")
    if event == "push":
        branch = payload.get("ref", "").removeprefix("refs/heads/")
    patterns = config["filters"]["branches"]
    if (
        branch
        and patterns
        and not any(fnmatchcase(branch, item) for item in patterns)
    ):
        return None, "Ветка исключена конфигурацией"
    state = action
    if event in {"create", "delete"}:
        state = "created" if event == "create" else "deleted"
    if merged:
        state = "merged"
    elif event == "issues" and action == "closed":
        state = "issue_closed"
    elif event == "pull_request_review" and action != "dismissed":
        state = review.get("state", "commented").lower()
    elif event.endswith("comment") and action == "created":
        state = "commented"
    elif event in {"check_run", "check_suite"}:
        state = payload[event].get("conclusion") or action
    style = config["styles"].get(state, config["styles"]["default"])
    title = (
        subject.get("title")
        or subject.get("name")
        or payload.get("ref")
        or repository
    )
    number = subject.get("number")
    prefix = f"#{number} " if number else ""
    body = (
        comment.get("body") or review.get("body") or subject.get("body") or ""
    )
    url = (
        comment.get("html_url")
        or review.get("html_url")
        or subject.get("html_url")
    )
    details = []
    for field in ("label", "assignee", "requested_reviewer", "member"):
        item = payload.get(field) or {}
        if item:
            details.append(text(item.get("name") or item.get("login"), 150))
    if event == "push":
        commits = [
            commit
            for commit in payload.get("commits", [])
            if not is_ignored(
                {
                    "login": commit.get("author", {}).get("username")
                    or commit.get("author", {}).get("name"),
                    "type": commit.get("author", {}).get("type"),
                },
                config,
            )
        ]
        if not commits:
            return None, "Отправка не содержит отображаемых коммитов"
        title = f"{branch}: {len(commits)} коммитов в полученном событии"
        details += [
            text(commit.get("message", "").split("\n")[0], 150)
            for commit in commits[: config["display"]["max_commits"]]
        ]
        if len(commits) > config["display"]["max_commits"]:
            details.append("Остальные коммиты — по ссылке на сравнение.")
        url = payload.get("compare")
    elif event == "commit_comment":
        title = (
            f"Комментарий к коммиту {str(comment.get('commit_id', ''))[:12]}"
        )
    elif event == "fork":
        title = payload.get("forkee", {}).get("full_name", repository)
    if branch:
        details.append(f"Ветка: {text(branch, 150)}")
    if event == "push" and payload.get("forced"):
        details.append("История ветки перезаписана принудительно.")
    description = text(body, config["display"]["body_length"]) if body else ""
    if details:
        description += "\n" + "\n".join(details)
    heading = text(f"{style['emoji']} {style['label']} · {prefix}{title}", 256)
    summary = (
        f"{event}/{action} · {repository} · {prefix}{title} · {style['label']}"
    )
    embed = {
        "title": heading,
        "url": safe_url(url, repository),
        "description": truncate(description, 3500)
        if description
        else style["label"],
        "color": int(style["color"].removeprefix("#"), 16),
        "fields": [
            {
                "name": "Кто выполнил действие",
                "value": text(sender.get("login"), 100),
            }
        ],
        "footer": {
            "text": text(
                config["display"]["repository_label"] or repository, 200
            )
        },
    }
    author = subject.get("user", {}).get("login")
    if author:
        embed["fields"].append({"name": "Автор", "value": text(author, 100)})
    return {
        "username": text(config["display"]["username"], 80),
        "embeds": [embed],
        "allowed_mentions": {"parse": []},
    }, summary
