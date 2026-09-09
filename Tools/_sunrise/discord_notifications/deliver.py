"""Сбор сохранённых событий и доставка постоянной очереди в Discord."""

import html
import os
import re
import sys
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path

from .config import load_config
from .content import split_message
from .formatting import format_event
from .github import GitHub, GitHubError, State
from .metadata import enrich_event
from .transport import (
    DiscordError,
    DiscordPublishTimeoutError,
    UnexpectedDiscordStatusError,
    send_message,
)

COLLECTOR = "sunrise-discord-events.yml"


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


class Journal:
    def __init__(self) -> None:
        self.lines = ["# Доставка уведомлений GitHub → Discord", ""]

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
        self.lines.append(f"- {html.escape(message)}")

    def finish(self) -> None:
        summary = os.environ.get("GITHUB_STEP_SUMMARY")
        if summary:
            with Path(summary).open("a", encoding="utf-8") as stream:
                stream.write("\n".join(self.lines) + "\n")


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
    canonical = "pull_request" if event == "pull_request_target" else event
    targets = [
        name
        for name, target in config["destinations"].items()
        if canonical not in target["excluded_events"]
        and (target["required"] or os.environ.get(target["webhook_env"]))
    ]
    if not targets:
        log(f"Пропущено: для {explanation} не включён ни один получатель.")
        return
    parts = split_message(message)
    created_at = utc_now()
    for index, part in enumerate(parts):
        part_key = key if len(parts) == 1 else f"{key}:part{index + 1:05d}"
        state.data["pending"].setdefault(
            part_key,
            {
                "message": part,
                "explanation": (
                    f"{explanation} · часть {index + 1}/{len(parts)}"
                ),
                "targets": targets,
                "sent": {},
                "created_at": created_at,
            },
        )
    log(
        f"Сохранено в очередь: {explanation}. "
        f"Получатели: {', '.join(targets)}."
    )


def collect(state: State, config: dict, log: Journal, deadline: float) -> int:
    github = state.github
    cutoff = (datetime.now(timezone.utc) - timedelta(minutes=5)).strftime(
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
            payload.pop("_discord_checks", None)
            if format_event(run["event"], payload, config)[0] is not None:
                enrich_event(run["event"], payload, github, config, log)
            enqueue(state, key, run["event"], payload, config, log)
            if key in state.data["pending"]:
                state.data["pending"][key]["created_at"] = run["created_at"]
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


def collect_commit_comments(state: State, config: dict, log: Journal) -> None:
    if not config["events"].get("commit_comment"):
        return
    github = state.github
    for comment in github.pages(f"{github.root}/comments"):
        key = str(comment["id"])
        stamp = comment.get("updated_at") or comment["created_at"]
        previous = state.data["commit_comments"].get(key)
        if previous == stamp:
            continue
        if previous or stamp >= state.data["cursor"]:
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
    state.save()


def deliver_pending(
    state: State,
    config: dict,
    log: Journal,
    deadline: float,
    *,
    force_retry: bool = False,
) -> int:
    failures = 0
    sent_count = 0
    attempted_count = 0
    pending = sorted(
        state.data["pending"].items(),
        key=lambda entry: (entry[1]["created_at"], entry[0]),
    )
    for key, item in pending:
        for name in item["targets"]:
            if name in item["sent"]:
                continue
            retry = item.setdefault("retry", {}).get(name, {})
            if not force_retry and retry.get("after", 0) > time.time():
                log(
                    f"Сообщение {key} для {name} ожидает назначенного повтора."
                )
                continue
            if (
                time.monotonic() >= deadline - 30
                or attempted_count
                >= config["delivery"]["max_messages_per_run"]
            ):
                log(
                    "Остаток очереди сохранён; "
                    "следующий запуск продолжит отправку."
                )
                return failures
            attempted_count += 1
            target = config["destinations"].get(name)
            address = (
                os.environ.get(target["webhook_env"], "") if target else ""
            )
            log(f"Сообщение {key}; получатель {name}; {item['explanation']}.")
            if not address:
                failures += 1
                log(
                    f"Ошибка: не задан секрет получателя {name}. "
                    "Сообщение остаётся в очереди."
                )
                defer(state, item, name, config)
                continue
            try:
                receipt = send_message(
                    address,
                    item["message"],
                    attempts=config["delivery"]["attempts"],
                    timeout=config["delivery"]["request_timeout"],
                    deadline=min(
                        deadline - 20,
                        time.monotonic()
                        + config["delivery"]["message_timeout"],
                    ),
                    report=log,
                )
            except (
                DiscordError,
                DiscordPublishTimeoutError,
                UnexpectedDiscordStatusError,
                ValueError,
            ) as error:
                failures += 1
                log(
                    f"Ошибка отправки: {error}. "
                    "Сообщение сохранено для следующего запуска."
                )
                defer(state, item, name, config)
                continue
            item["sent"][name] = {
                "message_id": receipt.get("id"),
                "at": utc_now(),
            }
            state.save()
            sent_count += 1
            log(
                f"Успешно отправлено в {name}: {item['explanation']}. "
                f"ID сообщения: {receipt.get('id', 'не предоставлен')}."
            )
            for embed in item["message"]["embeds"]:
                author_name = embed.get("author", {}).get("name", "")
                heading = embed.get("title") or embed.get("author", {}).get(
                    "name", "Изображение"
                )
                log(
                    f"Карточка: {heading}. "
                    f"Автор/проверка: {author_name}. "
                    f"Текст: {embed.get('description', '')} "
                    f"Ссылка: {embed.get('url', '')} "
                    f"Изображение: {embed.get('image', {}).get('url', '')}"
                )
                for field in embed.get("fields", []):
                    log(f"{field['name']}: {field['value']}")
        if all(name in item["sent"] for name in item["targets"]):
            del state.data["pending"][key]
            state.save()
    log(
        f"Итог: отправлено {sent_count}; ошибок {failures}; "
        f"сообщений в очереди {len(state.data['pending'])}."
    )
    return failures


def defer(state: State, item: dict, name: str, config: dict) -> None:
    attempts = (
        item.setdefault("retry", {}).get(name, {}).get("attempts", 0) + 1
    )
    delay = min(
        config["delivery"]["retry_interval"] * 2 ** min(attempts - 1, 12),
        config["delivery"]["max_retry_interval"],
    )
    item["retry"][name] = {"attempts": attempts, "after": time.time() + delay}
    state.save()


def main() -> int:
    log = Journal()
    try:
        config = load_config(Path(__file__).with_name("config.toml"))
        github = GitHub(
            os.environ["GITHUB_REPOSITORY"], os.environ["GITHUB_TOKEN"]
        )
        deadline = time.monotonic() + config["delivery"]["run_timeout"]
        initial = (
            datetime.now(timezone.utc)
            - timedelta(hours=config["delivery"]["bootstrap_hours"])
        ).strftime("%Y-%m-%dT%H:%M:%SZ")
        state = State(github, config["delivery"]["state_branch"], initial)
        log(
            "Состояние очереди прочитано. Уже подтверждённые получатели "
            "не будут отправлены повторно."
        )
        errors = 0
        try:
            collect_commit_comments(state, config, log)
            errors += collect(state, config, log, deadline)
        except (GitHubError, ValueError, KeyError, TypeError) as error:
            log(
                f"Ошибка сбора: {error}. "
                "Попробуем доставить ранее сохранённые сообщения."
            )
            errors += 1
            state.save()
        manual = os.environ.get("GITHUB_EVENT_NAME") == "workflow_dispatch"
        failed = deliver_pending(
            state,
            config,
            log,
            deadline,
            force_retry=manual,
        )
        return int(failed + errors > 0)
    except Exception as error:
        log(
            f"Работа остановлена: {type(error).__name__}: {error}. "
            "Проверьте настройки и доступ; сохранённая очередь не удаляется."
        )
        return 1
    finally:
        log.finish()


if __name__ == "__main__":
    sys.exit(main())
