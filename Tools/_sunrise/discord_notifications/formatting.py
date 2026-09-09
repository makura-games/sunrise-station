"""Преобразование недоверенных данных GitHub в карточки Discord."""

import re
from fnmatch import fnmatchcase
from urllib.parse import urlsplit

from .content import body_base, prepare_body, split_body


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


def plain(value: object) -> str:
    return re.sub(r"[\x00-\x08\x0b-\x1f\x7f]", "", str(value or ""))


def avatar_url(value: object) -> str:
    value = str(value or "")
    parsed = urlsplit(value)
    if (
        parsed.scheme == "https"
        and parsed.netloc
        in {
            "avatars.githubusercontent.com",
            "raw.githubusercontent.com",
            "github.com",
            "cdn.discordapp.com",
        }
        and not re.search(r"[\s<>\x00-\x1f]", value)
    ):
        return value
    return ""


def author(account: dict) -> dict:
    login = plain(account.get("login"))
    result = {"name": truncate(login, 256)}
    profile = str(account.get("html_url", ""))
    parsed = urlsplit(profile)
    if parsed.scheme == "https" and parsed.netloc == "github.com":
        result["url"] = profile
    icon = avatar_url(account.get("avatar_url"))
    if icon:
        result["icon_url"] = icon
    return result


def set_color(embed: dict, style: dict) -> None:
    embed.pop("color", None)
    if style["color"]:
        embed["color"] = int(style["color"].removeprefix("#"), 16)


def conflict_fields(subject: dict, config: dict) -> list[dict]:
    fields = []
    if subject.get("mergeable") is False:
        fields.append(
            {
                "name": config["mommi"]["status"],
                "value": config["mommi"]["conflicts"],
                "inline": True,
            }
        )
    return fields


def status_footer(payload: dict, subject: dict, config: dict) -> dict:
    status = payload.get("_discord_pr_status")
    if isinstance(status, dict):
        state = str(status.get("state", "unknown")).lower()
        draft = status.get("isDraft")
        decision = status.get("reviewDecision")
        review = str(decision).lower() if decision else "none"
    else:
        state = (
            "merged"
            if subject.get("merged")
            else subject.get("state", "unknown")
        )
        draft = subject.get("draft")
        review = "unknown"
    if state == "open" and draft:
        state = "draft"
    pr_label = config["pr_status"].get(state, config["pr_status"]["unknown"])
    review_label = config["review_status"].get(
        review, config["review_status"]["unknown"]
    )
    return {"text": f"PR: {pr_label} · Review: {review_label}"}


def check_cards(checks: list[dict], url: str, config: dict) -> list[dict]:
    rows = []
    for check in checks:
        status = check.get("status")
        if status == "completed":
            status = check.get("conclusion")
        icon = "neutral"
        if status == "success":
            icon = "approved"
        elif status in {
            "failure",
            "error",
            "timed_out",
            "action_required",
            "stale",
        }:
            icon = "failure"
        elif status in {
            "queued",
            "in_progress",
            "pending",
            "waiting",
            "requested",
        }:
            icon = "pending"
        name = text(plain(check.get("name")).replace("\n", " "), 400)
        rows.append(f"{config['icons'][icon]} {name}")
    return [
        {"title": config["mommi"]["checks"], "url": url, "description": chunk}
        for chunk in split_body("\n".join(rows), 4096)
    ]


def render_embed(
    event: str,
    action: str,
    payload: dict,
    subject: dict,
    state: str,
    config: dict,
) -> dict:
    repository = payload["repository"]["full_name"]
    sender = payload.get("sender") or {}
    number = subject.get("number")
    title = plain(subject.get("title") or subject.get("name") or repository)
    style = config["styles"].get(state, config["styles"]["default"])
    comment = payload.get("comment") or payload.get("review") or {}
    body = comment.get("body") if comment else subject.get("body")
    embed = {
        "title": f"{style['emoji']} {title}".strip(),
        "url": safe_url(
            comment.get("html_url") or subject.get("html_url"), repository
        ),
        "description": plain(body).strip(),
    }
    displayed_author = comment.get("user") or sender
    if displayed_author and displayed_author.get("login"):
        embed["author"] = author(displayed_author)
    set_color(embed, style)
    if event in {"pull_request", "issues"}:
        votes = []
        if config["display"]["show_reactions"]:
            reactions = subject.get("reactions") or {}
            for key, name in (("+1", "upvote"), ("-1", "downvote")):
                count = reactions.get(key, 0)
                if type(count) is int and count > 0:
                    votes.append(f"{config['mommi'][name]} {count}")
        embed["description"] += "\n" + "   ".join(votes) + "\u200b"
        if event == "pull_request" and config["display"]["show_checks"]:
            fields = conflict_fields(subject, config)
            if fields:
                embed["fields"] = fields
    elif event == "pull_request_review":
        style = config["reviews"].get(state, config["reviews"]["commented"])
        set_color(embed, style)
        embed["title"] = (
            f"{config['icons'][style['icon']]} {style['label']} · {title}"
        ).strip()
    elif event == "pull_request_review_comment":
        set_color(embed, config["styles"]["review_comment"])
        review_state = payload.get("_discord_review_state")
        review_label = (
            config["reviews"]
            .get(review_state, {})
            .get("label", config["review_status"]["unknown"])
        )
        prefix = config["mommi"]["review_comment"]
        if action in {"edited", "deleted"}:
            prefix = config["mommi"][f"{action}_review_comment"]
        embed["title"] = (
            f"{config['icons']['review']} {prefix} · {review_label} "
            f"· #{number} {title}"
        ).strip()
        path = plain(comment.get("path"))
        line = comment.get("line") or comment.get("original_line")
        if path:
            location = f"{path}:{line}" if line else path
            embed["fields"] = [
                {
                    "name": config["mommi"]["file"],
                    "value": text(location, 1000),
                    "inline": False,
                }
            ]
    elif event in {
        "issue_comment",
        "commit_comment",
    }:
        prefix = config["mommi"]["new_comment"]
        if action in {"edited", "deleted"}:
            prefix = config["mommi"][f"{action}_comment"]
        if event == "commit_comment":
            title = str(comment.get("commit_id", ""))[:7]
            target = config["mommi"]["commit"] + " " + title
        else:
            kind = (
                "pull_request"
                if (
                    payload.get("pull_request")
                    or subject.get("pull_request")
                    or event == "pull_request_review_comment"
                )
                else "issue"
            )
            target = f"{config['mommi'][kind]} #{number}: {title}"
        embed["title"] = (
            f"{config['icons']['comment']} [{repository.split('/')[-1]}] "
            f"{prefix} {target}"
        ).strip()
        embed.pop("color", None)
        set_color(embed, config["styles"]["commented"])
    elif event in {"discussion", "discussion_comment"}:
        action_label = config["mommi"].get(f"discussion_{action}", action)
        if event == "discussion_comment":
            action_label = config["mommi"]["discussion_commented"]
        else:
            embed["description"] += "\n"
            if action != "created":
                embed.pop("author", None)
        embed["title"] = (
            f"{config['mommi']['discussion']} {action_label}: {title}"
        )
        if action == "created" or event == "discussion_comment":
            embed.pop("color", None)
    elif event == "push":
        commits = payload["commits"]
        count = len(commits)
        noun = "commit_singular" if count == 1 else "commit_plural"
        ref = plain(payload.get("ref"))
        embed["title"] = (
            f"**{count}** {config['mommi'][noun]} "
            f"{config['mommi']['push_to']} **{ref}**"
        )
        embed["url"] = safe_url(payload.get("compare"), repository)
        if payload.get("forced"):
            embed["title"] = (
                config["mommi"]["force_push"] + " " + embed["title"]
            )
            set_color(embed, config["styles"]["force_push"])
        lines = []
        limit = config["display"]["commit_length"]
        for commit in commits[: config["display"]["max_commits"]]:
            message = plain(commit.get("message"))
            if len(message) > limit:
                message = message[:limit] + config["mommi"]["ellipsis"]
            sha = str(commit.get("id", ""))[:7]
            url = safe_url(commit.get("url"), repository)
            lines.append(f"[`{sha}`]({url}) {message}\n")
        if count >= config["display"]["max_commits"]:
            lines.append(config["mommi"]["overflow"])
        embed["description"] = "".join(lines)
    elif event in {"create", "delete"}:
        embed["title"] = (
            f"{style['emoji']} {plain(payload.get('ref'))}".strip()
        )
    elif event == "fork":
        fork = payload.get("forkee", {}).get("full_name", title)
        embed["title"] = f"{style['emoji']} {fork}"
    embed["title"] = truncate(embed["title"], 256)
    if (
        event == "push"
        and len(embed["description"].encode("utf-16-le")) // 2 > 3500
    ):
        embed["description"] = truncate(embed["description"], 3500)
    return embed


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
    if (
        event == "pull_request_review"
        and action != "dismissed"
        and str(review.get("state", "")).lower() == "commented"
        and not plain(review.get("body")).strip()
    ):
        return None, (
            "Общий текст ревью пуст. Замечания к строкам кода "
            "обрабатываются отдельными событиями pull_request_review_comment"
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
    if event == "push":
        state = "push"
    if event in {"create", "delete"}:
        state = "created" if event == "create" else "deleted"
    if merged:
        state = "merged"
    elif event == "issues" and action == "closed":
        state = "issue_closed"
    elif event == "pull_request_review" and action != "dismissed":
        state = review.get("state", "commented").lower()
    elif event == "pull_request_review_comment":
        state = "review_comment"
    elif event.endswith("comment") and action == "created":
        state = "commented"
    elif event in {"check_run", "check_suite"}:
        state = payload[event].get("conclusion") or action
    if event in {"pull_request", "issues"}:
        closed = action == "closed" or subject.get("state") == "closed"
        if event == "pull_request":
            state = (
                "merged"
                if pull.get("merged")
                else "closed"
                if closed
                else "opened"
            )
        else:
            state = "issue_closed" if closed else "issue_opened"
    style = config["styles"].get(state, config["styles"]["default"])
    title = (
        subject.get("title")
        or subject.get("name")
        or payload.get("ref")
        or repository
    )
    number = subject.get("number")
    prefix = f"#{number} " if number else ""
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
        payload = {**payload, "commits": commits}
    summary = (
        f"{event}/{action} · {repository} · {prefix}{title} · {style['label']}"
    )
    embed = render_embed(event, action, payload, subject, state, config)
    is_pull = event in {
        "pull_request",
        "pull_request_review",
        "pull_request_review_comment",
        "issue_comment",
    } and bool(pull or issue.get("pull_request"))
    if is_pull:
        embed["footer"] = status_footer(payload, subject, config)
    body, images = prepare_body(
        embed["description"], body_base(repository, subject)
    )
    chunks = split_body(body, config["display"]["body_length"])
    embeds = []
    fields = embed.pop("fields", [])
    for index, chunk in enumerate(chunks):
        part = {**embed, "description": chunk}
        if index:
            part["title"] = truncate(
                f"{embed['title']} ({index + 1}/{len(chunks)})", 256
            )
        embeds.append(part)
    for index, address in enumerate(images):
        if index == 0:
            embeds[-1]["image"] = {"url": address}
        else:
            embeds.append({"image": {"url": address}})
    if fields:
        embeds[0]["fields"] = fields
    checks = payload.get("_discord_checks", [])
    if event == "pull_request" and config["display"]["show_checks"] and checks:
        embeds.extend(
            check_cards(
                checks,
                safe_url(subject.get("html_url"), repository) + "/checks",
                config,
            )
        )
    message = {
        "username": truncate(plain(config["display"]["username"]), 80),
        "embeds": embeds,
        "allowed_mentions": {"parse": []},
    }
    icon = avatar_url(config["display"]["avatar_url"])
    if icon:
        message["avatar_url"] = icon
    return message, summary
