"""Преобразование недоверенных данных GitHub в карточки Discord."""

import re
from fnmatch import fnmatchcase
from urllib.parse import quote, urlsplit

from .content import add_code_language, body_base, prepare_body, split_body


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


def link_text(value: object, limit: int = 1000) -> str:
    value = plain(value).replace("@", "＠")
    value = value.replace("[", "［").replace("]", "］")
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


def repository_file_url(
    repository: str, revision: object, path: object, line: object
) -> str:
    revision = str(revision or "")
    path = str(path or "")
    parts = path.split("/")
    if (
        not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository)
        or not re.fullmatch(r"[0-9a-fA-F]{40}", revision)
        or not parts
        or any(part in {"", ".", ".."} for part in parts)
    ):
        return ""
    encoded_path = "/".join(quote(part, safe="") for part in parts)
    address = f"https://github.com/{repository}/blob/{revision}/{encoded_path}"
    if type(line) is int and line > 0:
        address += f"#L{line}"
    return address


def is_ignored(account: dict, config: dict) -> bool:
    filters = config["filters"]
    login = str(account.get("login", "")).casefold()
    return (
        filters["ignore_bots"]
        and (account.get("type") == "Bot" or login.endswith("[bot]"))
        or login in {name.casefold() for name in filters["ignored_users"]}
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


def status_footer(payload: dict, subject: dict, config: dict) -> dict:
    status = payload.get("_discord_pr_status")
    if isinstance(status, dict):
        state = str(status.get("state", "unknown")).lower()
        draft = status.get("isDraft")
        decision = status.get("reviewDecision")
        review = str(decision).lower() if decision else "none"
        conflicts = status.get("mergeable") == "CONFLICTING"
    else:
        state = (
            "merged"
            if subject.get("merged")
            else subject.get("state", "unknown")
        )
        draft = subject.get("draft")
        review = "unknown"
        conflicts = subject.get("mergeable") is False
    if state == "open" and draft:
        state = "draft"
    pr_label = config["pr_status"].get(state, config["pr_status"]["unknown"])
    review_label = config["review_status"].get(
        review, config["review_status"]["unknown"]
    )
    footer = (
        f"{config['text']['pull_request']}: {pr_label} · "
        f"{config['text']['review']}: {review_label}"
    )
    if conflicts:
        footer += " · " + config["pr_status"]["conflicts"]
    return {"text": footer}


def render_embed(
    event: str,
    action: str,
    payload: dict,
    subject: dict,
    style: dict,
    config: dict,
) -> dict:
    repository = payload["repository"]["full_name"]
    sender = payload.get("sender") or {}
    number = subject.get("number")
    title = plain(subject.get("title") or subject.get("name") or repository)
    icon = config["icons"].get(style.get("icon", ""), "")
    comment = payload.get("comment") or payload.get("review") or {}
    body = comment.get("body") if comment else subject.get("body")
    if event == "pull_request_review_comment":
        body = add_code_language(str(body or ""), comment.get("path"))
    embed = {
        "title": f"{icon} {title}".strip(),
        "url": safe_url(
            comment.get("html_url") or subject.get("html_url"), repository
        ),
        "description": plain(body).strip(),
    }
    displayed_author = (
        subject.get("user")
        if event == "pull_request"
        else comment.get("user") or sender
    )
    if displayed_author and displayed_author.get("login"):
        embed["author"] = author(displayed_author)
    set_color(embed, style)
    if event in {"pull_request", "issues"}:
        if action == "reopened":
            kind = "pull_request" if event == "pull_request" else "issue"
            closed_icon = (
                "closed" if event == "pull_request" else "issue_closed"
            )
            opened_icon = (
                "opened" if event == "pull_request" else "issue_opened"
            )
            embed["title"] = (
                f"{config['icons'][closed_icon]} → "
                f"{config['icons'][opened_icon]} "
                f"{config['text'][f'{kind}_reopened']} · {title}"
            )
        votes = []
        if config["display"]["show_reactions"]:
            reactions = subject.get("reactions") or {}
            for key, name in (("+1", "upvote"), ("-1", "downvote")):
                count = reactions.get(key, 0)
                if type(count) is int and count > 0:
                    votes.append(f"{config['icons'][name]} {count}")
        embed["description"] += "\n" + "   ".join(votes) + "\u200b"
    elif event == "pull_request_review":
        embed["title"] = f"{icon} {title}".strip()
    elif event == "pull_request_review_comment":
        if comment.get("in_reply_to_id") is not None:
            prefix = config["text"]["new_comment"]
            if action in {"edited", "deleted"}:
                prefix = config["text"][f"{action}_comment"]
            embed["title"] = (
                f"{config['icons']['comment']} "
                f"[{repository.split('/')[-1]}] {prefix} "
                f"{config['text']['pull_request']} #{number}: {title}"
            ).strip()
        else:
            prefix = config["text"]["review_comment"]
            if action in {"edited", "deleted"}:
                prefix = config["text"][f"{action}_review_comment"]
            embed["title"] = f"{icon} {prefix} · #{number} {title}".strip()
            path = plain(comment.get("path"))
            revision = comment.get("original_commit_id")
            line = comment.get("original_line")
            if not revision:
                revision = comment.get("commit_id")
                line = comment.get("line")
            if path:
                location = f"{path}:{line}" if line else path
                address = repository_file_url(
                    repository, revision, comment.get("path"), line
                )
                value = link_text(location)
                if address:
                    value = f"[{value}]({address})"
                embed["fields"] = [
                    {
                        "name": config["text"]["file"],
                        "value": value,
                        "inline": False,
                    }
                ]
    elif event in {
        "issue_comment",
        "commit_comment",
    }:
        prefix = config["text"]["new_comment"]
        if action in {"edited", "deleted"}:
            prefix = config["text"][f"{action}_comment"]
        if event == "commit_comment":
            title = str(comment.get("commit_id", ""))[:7]
            target = config["text"]["commit"] + " " + title
        else:
            kind = (
                "pull_request"
                if (payload.get("pull_request") or subject.get("pull_request"))
                else "issue"
            )
            target = f"{config['text'][kind]} #{number}: {title}"
        embed["title"] = (
            f"{config['icons']['comment']} [{repository.split('/')[-1]}] "
            f"{prefix} {target}"
        ).strip()
        embed.pop("color", None)
        set_color(embed, config["styles"]["commented"])
    elif event in {"discussion", "discussion_comment"}:
        action_text = config["text"].get(f"discussion_{action}", action)
        if event == "discussion_comment":
            action_text = config["text"][f"discussion_comment_{action}"]
        else:
            embed["description"] += "\n"
            if action != "created":
                embed.pop("author", None)
        embed["title"] = (
            f"{config['icons']['discussion']} {action_text}: {title}"
        )
        if action == "created" or event == "discussion_comment":
            embed.pop("color", None)
    elif event == "push":
        commits = payload["commits"]
        count = len(commits)
        ref = plain(payload.get("ref")).removeprefix("refs/heads/")
        embed["title"] = (
            f"{config['text']['commits']}: **{count}** · "
            f"{config['text']['branch']}: **{ref}**"
        )
        embed["url"] = safe_url(payload.get("compare"), repository)
        if payload.get("forced"):
            embed["title"] = (
                config["text"]["force_push"] + " " + embed["title"]
            )
        lines = []
        limit = config["display"]["commit_length"]
        for commit in commits[: config["display"]["max_commits"]]:
            message = plain(commit.get("message"))
            if len(message) > limit:
                message = message[:limit] + config["text"]["ellipsis"]
            sha = str(commit.get("id", ""))[:7]
            url = safe_url(commit.get("url"), repository)
            lines.append(f"[`{sha}`]({url}) {message}\n")
        if count > config["display"]["max_commits"]:
            lines.append(config["text"]["overflow"])
        embed["description"] = "".join(lines)
    elif event == "delete":
        embed["title"] = f"{icon} {plain(payload.get('ref'))}".strip()
    elif event == "fork":
        fork = payload.get("forkee", {}).get("full_name", title)
        embed["title"] = f"{icon} {fork}"
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
    if event == "push" and payload.get("ref") != "refs/heads/master":
        return None, "Уведомления о коммитах разрешены только для master"
    related_pr = payload.get("_discord_related_pr")
    if event == "delete" and type(related_pr) is int:
        return None, f"Удалённая ветка связана с PR #{related_pr}"
    if event == "pull_request" and action == "synchronize":
        return None, "Обновление коммитов PR отдельно не публикуется"
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
        state = "force_push" if payload.get("forced") else "push"
    elif event == "delete":
        state = "deleted"
    elif event == "pull_request_review":
        state = review.get("state", "commented").lower()
    elif event == "pull_request_review_comment":
        state = (
            "commented"
            if comment.get("in_reply_to_id") is not None
            else str(payload.get("_discord_review_state", "commented")).lower()
        )
    elif event.endswith("comment") and action == "created":
        state = "commented"
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
    if event == "pull_request_review" or (
        event == "pull_request_review_comment"
        and comment.get("in_reply_to_id") is None
    ):
        style = config["reviews"].get(state, config["reviews"]["commented"])
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
    summary = f"{event}/{action} · {repository} · {prefix}{title}"
    embed = render_embed(event, action, payload, subject, style, config)
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
    message = {
        "username": truncate(plain(config["display"]["username"]), 80),
        "embeds": embeds,
        "allowed_mentions": {"parse": []},
    }
    icon = avatar_url(config["display"]["avatar_url"])
    if icon:
        message["avatar_url"] = icon
    return message, summary
