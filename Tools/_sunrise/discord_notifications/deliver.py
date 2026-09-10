"""Сбор сохранённых событий и доставка постоянной очереди в Discord."""

import argparse
import html
import os
import re
import sys
import time
from datetime import UTC, datetime, timedelta
from pathlib import Path

from .config import load_config
from .content import split_message
from .formatting import format_event
from .github import GitHub, GitHubError
from .metadata import enrich_event
from .state import State
from .transport import (
    DiscordError,
    DiscordPublishTimeoutError,
    UnexpectedDiscordStatusError,
    send_message,
)

COLLECTOR = "sunrise-discord-events.yml"
EDIT_ACTIONS = {
    "pull_request": {"edited", "ready_for_review", "converted_to_draft"},
    "issues": {"edited"},
    "issue_comment": {"edited", "deleted"},
    "pull_request_review_comment": {"edited", "deleted"},
    "discussion": {"edited", "deleted", "answered", "unanswered"},
    "discussion_comment": {"edited", "deleted"},
    "commit_comment": {"edited"},
}


def utc_now() -> str:
    return datetime.now(UTC).strftime("%Y-%m-%dT%H:%M:%SZ")


def message_reference(event: str, payload: dict) -> tuple[str, bool, bool]:
    event = "pull_request" if event == "pull_request_target" else event
    action = payload.get("action", event)
    source = {
        "pull_request": payload.get("pull_request"),
        "issues": payload.get("issue"),
        "discussion": payload.get("discussion"),
        "issue_comment": payload.get("comment"),
        "pull_request_review_comment": payload.get("comment"),
        "discussion_comment": payload.get("comment"),
        "commit_comment": payload.get("comment"),
    }.get(event)
    identifier = None
    if source:
        field = (
            "number"
            if event in {"pull_request", "issues", "discussion"}
            else "id"
        )
        identifier = source.get(field)
    key = (
        f"{event}:{identifier}"
        if type(identifier) is int and identifier > 0
        else ""
    )
    editing = action in EDIT_ACTIONS.get(event, set())
    return key, editing, editing and action == "deleted"


class Journal:
    def __init__(self) -> None:
        self.lines = ["# Доставка уведомлений GitHub → Discord", ""]
        self.summary_size = 0
        self.summary_truncated = False

    def __call__(self, message: str) -> None:
        message = re.sub(
            r"https://[^\s]*?/api(?:/v\d+)?/webhooks/[^\s]+",
            "[секрет скрыт]",
            message,
        )
        message = re.sub(r"[\x00-\x1f\x7f]", " ", message)
        message = re.sub(r"\\([\\`*_{}\[\]()<>|~])", r"\1", message)
        message = message[:4000]
        print(f"[Discord] {message}", flush=True)
        line = f"- {html.escape(message)}"
        size = len(line.encode("utf-8")) + 1
        if self.summary_size + size <= 900_000:
            self.lines.append(line)
            self.summary_size += size
        else:
            self.summary_truncated = True

    def finish(self) -> None:
        summary = os.environ.get("GITHUB_STEP_SUMMARY")
        if summary:
            with Path(summary).open("a", encoding="utf-8") as stream:
                stream.write("\n".join(self.lines) + "\n")
                if self.summary_truncated:
                    stream.write(
                        "\nСводка сокращена из-за размера. "
                        "Все последующие записи доступны в журнале шага.\n"
                    )


def enqueue(
    state: State,
    key: str,
    event: str,
    payload: dict,
    config: dict,
    log: Journal,
) -> None:
    if (
        not isinstance(payload, dict)
        or payload.get("repository", {}).get("full_name")
        != state.github.repository
    ):
        raise ValueError(
            "Событие принадлежит другому репозиторию или повреждено"
        )
    message, explanation = format_event(event, payload, config)
    if message is None:
        log(f"Пропущено: {explanation}.")
        return
    parts = split_message(message)
    record_key, editing, forget = message_reference(event, payload)
    messages = state.data.setdefault("messages", {})
    message_ids = messages.get(record_key, []) if editing else []
    created_at = utc_now()
    for index, part in enumerate(parts):
        part_key = key if len(parts) == 1 else f"{key}:part{index + 1:05d}"
        pending = {
            "message": part,
            "explanation": (f"{explanation} · часть {index + 1}/{len(parts)}"),
            "created_at": created_at,
        }
        if record_key:
            pending["record_key"] = record_key
            pending["part_index"] = index
            pending["part_count"] = len(parts)
        if editing:
            pending["forget_record"] = forget
            if index < len(message_ids) and message_ids[index]:
                pending["message_id"] = message_ids[index]
        state.data["pending"].setdefault(
            part_key,
            pending,
        )
    if editing:
        for index, message_id in enumerate(
            message_ids[len(parts) :], start=len(parts)
        ):
            if not message_id:
                continue
            state.data["pending"].setdefault(
                f"{key}:remove{index + 1:05d}",
                {
                    "message": {"embeds": []},
                    "message_id": message_id,
                    "delete": True,
                    "record_key": record_key,
                    "part_count": len(parts),
                    "explanation": (
                        f"{explanation} · удаление лишней части {index + 1}"
                    ),
                    "created_at": created_at,
                },
            )
    operation = "изменение" if editing else "сообщение"
    log(f"Сохранено в очередь {operation}: {explanation}.")


def collect(state: State, config: dict, log: Journal, deadline: float) -> int:
    github = state.github
    cutoff = (datetime.now(UTC) - timedelta(minutes=5)).strftime(
        "%Y-%m-%dT%H:%M:%SZ"
    )
    cursor = state.data["cursor"]
    failures = []
    errors = 0
    runs = github.pages(
        f"{github.root}/actions/workflows/{COLLECTOR}/runs",
        "workflow_runs",
        exclude_pull_requests="false",
    )
    for run in runs:
        if run["created_at"] < cursor:
            break
        if time.monotonic() >= deadline - 60:
            failures.append(cursor)
            log(
                "Сбор продолжится следующим запуском: "
                "заканчивается время задачи."
            )
            break
        if (
            len(state.data["pending"])
            >= config["delivery"]["max_pending_messages"]
        ):
            failures.append(cursor)
            log(
                "Очередь заполнена. Новые события остаются в файлах GitHub; "
                "сначала доставим накопившиеся сообщения."
            )
            break
        key = f"run:{run['id']}"
        if key in state.data["seen"]:
            continue
        if run["status"] != "completed":
            failures.append(run["created_at"])
            log(f"Сбор события {run['id']} ещё не завершён; подождём.")
            continue
        if run["conclusion"] == "skipped":
            state.data["seen"][key] = run["created_at"]
            continue
        try:
            payload = github.event_artifact(run["id"])
            if payload.get("sender", {}).get("login") != run["actor"]["login"]:
                raise ValueError("Отправитель не совпадает с данными запуска")
            pull_numbers = {
                item["number"] for item in run.get("pull_requests", [])
            }
            number = payload.get("pull_request", {}).get("number")
            if pull_numbers and number not in pull_numbers:
                raise ValueError("Номер PR не совпадает с данными запуска")
            payload.pop("_discord_pr_status", None)
            payload.pop("_discord_review_state", None)
            if format_event(run["event"], payload, config)[0] is not None:
                enrich_event(run["event"], payload, github, config, log)
            enqueue(state, key, run["event"], payload, config, log)
            for pending_key, pending in state.data["pending"].items():
                if pending_key == key or pending_key.startswith(key + ":part"):
                    pending["created_at"] = run["created_at"]
        except (GitHubError, ValueError, KeyError, TypeError) as error:
            failures.append(run["created_at"])
            errors += 1
            log(
                f"Не получено событие запуска {run['id']}: {error}. "
                "Оно не отмечено доставленным."
            )
            if (
                run["conclusion"] in {"failure", "cancelled", "timed_out"}
                and run["run_attempt"] < 3
            ):
                github.json(
                    "POST", f"{github.root}/actions/runs/{run['id']}/rerun"
                )
                log(f"Повторно запущен сбор события {run['id']}.")
            continue
        state.data["seen"][key] = run["created_at"]
    next_cursor = min([cutoff, *failures])
    state.data["cursor"] = max(cursor, next_cursor)
    state.data["seen"] = {
        key: stamp
        for key, stamp in state.data["seen"].items()
        if stamp >= state.data["cursor"]
    }
    state.save()
    return errors


def collect_commit_comments(
    state: State, config: dict, log: Journal, deadline: float
) -> None:
    if not config["events"].get("commit_comment"):
        return
    github = state.github
    cursor = state.data.setdefault("comments_cursor", state.data["cursor"])
    page = state.data.get("comments_page", 1)
    while True:
        if (
            time.monotonic() >= deadline - 30
            or len(state.data["pending"])
            >= config["delivery"]["max_pending_messages"]
        ):
            log("Сбор комментариев продолжится со сохранённой страницы.")
            state.save()
            return
        comments = github.json(
            "GET",
            f"{github.root}/comments",
            params={"per_page": 100, "page": page},
        )
        for comment in comments:
            key = str(comment["id"])
            stamp = comment.get("updated_at") or comment["created_at"]
            previous = state.data["commit_comments"].get(key)
            if previous == stamp:
                continue
            if previous or stamp >= cursor:
                action = "edited" if previous else "created"
                enqueue(
                    state,
                    f"comment:{key}:{stamp}",
                    "commit_comment",
                    {
                        "repository": {"full_name": github.repository},
                        "action": action,
                        "comment": comment,
                        "sender": comment["user"],
                    },
                    config,
                    log,
                )
            state.data["commit_comments"][key] = stamp
        if len(comments) < 100:
            state.data.pop("comments_cursor", None)
            state.data.pop("comments_page", None)
            state.save()
            return
        page += 1
        state.data["comments_page"] = page
        state.save()


def deliver_pending(
    state: State,
    config: dict,
    log: Journal,
    deadline: float,
    *,
    force_retry: bool = False,
) -> int:
    address = os.environ.get("DISCORD_EVENTS_WEBHOOK", "")
    if not address:
        raise ValueError("Не задан секрет DISCORD_EVENTS_WEBHOOK")
    failures = 0
    sent_count = 0
    attempted_count = 0
    pending = sorted(
        state.data["pending"].items(),
        key=lambda entry: (entry[1]["created_at"], entry[0]),
    )
    for key, item in pending:
        if key not in state.data["pending"]:
            continue
        retry = item.get("retry", {})
        if not force_retry and retry.get("after", 0) > time.time():
            log(f"Сообщение {key} ожидает назначенного повтора.")
            continue
        if (
            time.monotonic() >= deadline - 30
            or attempted_count >= config["delivery"]["max_messages_per_run"]
        ):
            log(
                "Остаток очереди сохранён; "
                "следующий запуск продолжит отправку. "
                f"Отправлено: {sent_count}; ошибок: {failures}; "
                f"в очереди: {len(state.data['pending'])}."
            )
            return failures
        attempted_count += 1
        message_id = item.get("message_id")
        operation = "Изменение" if message_id else "Сообщение"
        log(f"{operation} {key}; {item['explanation']}.")
        try:
            receipt = send_message(
                address,
                item["message"],
                message_id=message_id,
                delete=item.get("delete", False),
                attempts=config["delivery"]["attempts"],
                timeout=config["delivery"]["request_timeout"],
                deadline=min(
                    deadline - 20,
                    time.monotonic() + config["delivery"]["message_timeout"],
                ),
                report=log,
            )
            if not receipt.get("id"):
                raise ValueError(
                    "Discord не вернул ID сообщения; доставка не подтверждена"
                )
        except (
            DiscordError,
            DiscordPublishTimeoutError,
            UnexpectedDiscordStatusError,
            ValueError,
        ) as error:
            if (
                message_id
                and isinstance(error, DiscordError)
                and error.status_code == 404
            ):
                record_key = item.get("record_key", "")
                state.data["messages"].pop(record_key, None)
                for pending_key, pending_item in list(
                    state.data["pending"].items()
                ):
                    if pending_item.get("record_key") == record_key:
                        del state.data["pending"][pending_key]
                state.save()
                log(
                    "Изменение пропущено: исходное сообщение Discord "
                    "удалено или принадлежит другому вебхуку."
                )
                continue
            failures += 1
            log(
                f"Ошибка отправки: {error}. "
                "Сообщение сохранено для следующего запуска."
            )
            defer(state, item, config)
            continue
        del state.data["pending"][key]
        record_key = item.get("record_key")
        if record_key:
            if item.get("forget_record"):
                state.data["messages"].pop(record_key, None)
            elif item.get("delete"):
                if record_key in state.data["messages"]:
                    state.data["messages"][record_key] = state.data[
                        "messages"
                    ][record_key][: item["part_count"]]
            else:
                part_count = item["part_count"]
                message_ids = state.data["messages"].get(record_key, [])
                message_ids = (message_ids[:part_count] + [None] * part_count)[
                    :part_count
                ]
                message_ids[item["part_index"]] = str(receipt["id"])
                state.data["messages"][record_key] = message_ids
        state.save()
        sent_count += 1
        operation = (
            "удалено"
            if item.get("delete")
            else "изменено" if message_id else "отправлено"
        )
        log(
            f"Успешно {operation}: {item['explanation']}. "
            f"ID сообщения: {receipt.get('id', 'не предоставлен')}."
        )
        for embed in item["message"]["embeds"]:
            author_name = embed.get("author", {}).get("name", "")
            heading = embed.get("title") or embed.get("author", {}).get(
                "name", "Изображение"
            )
            log(
                f"Карточка: {heading}. "
                f"Автор: {author_name}. "
                f"Текст: {embed.get('description', '')} "
                f"Ссылка: {embed.get('url', '')} "
                f"Изображение: {embed.get('image', {}).get('url', '')}"
            )
            for field in embed.get("fields", []):
                log(f"{field['name']}: {field['value']}")
            if embed.get("footer"):
                log(f"Статусы: {embed['footer']['text']}")
    log(
        f"Итог: отправлено {sent_count}; ошибок {failures}; "
        f"сообщений в очереди {len(state.data['pending'])}."
    )
    return failures


def defer(state: State, item: dict, config: dict) -> None:
    attempts = item.get("retry", {}).get("attempts", 0) + 1
    delay = min(
        config["delivery"]["retry_interval"] * 2 ** min(attempts - 1, 12),
        config["delivery"]["max_retry_interval"],
    )
    item["retry"] = {"attempts": attempts, "after": time.time() + delay}
    state.save()


def main(phase: str = "send") -> int:
    log = Journal()
    try:
        config = load_config(Path(__file__).with_name("config.toml"))
        github = GitHub(
            os.environ["GITHUB_REPOSITORY"], os.environ["GITHUB_TOKEN"]
        )
        deadline = time.monotonic() + config["delivery"]["run_timeout"] / 2
        state = State(github, Path(os.environ["DISCORD_STATE_PATH"]))
        log(
            "Состояние очереди прочитано. Уже доставленные сообщения "
            "не будут отправлены повторно."
        )
        if phase == "collect":
            try:
                collect_commit_comments(
                    state,
                    config,
                    log,
                    deadline - config["delivery"]["run_timeout"] * 0.25,
                )
                return int(collect(state, config, log, deadline) > 0)
            finally:
                state.save()
        manual = os.environ.get("GITHUB_EVENT_NAME") == "workflow_dispatch"
        failed = deliver_pending(
            state,
            config,
            log,
            deadline,
            force_retry=manual,
        )
        return int(failed > 0)
    except Exception as error:
        log(
            f"Работа остановлена: {type(error).__name__}: {error}. "
            "Проверьте настройки и доступ; сохранённая очередь не удаляется."
        )
        return 1
    finally:
        log.finish()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=("collect", "send"))
    sys.exit(main(parser.parse_args().phase))
