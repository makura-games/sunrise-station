"""Локальные снимки очереди и восстановление из артефактов Actions."""

import argparse
import json
import os
from datetime import UTC, datetime, timedelta
from pathlib import Path

from .config import load_config
from .github import GitHub, GitHubError

ARTIFACTS = ("discord-notification-queue", "discord-notification-state")
SEND_STEP = "Отправить очередь в Discord"


def validate(document: dict) -> None:
    if not isinstance(document, dict) or document.get("version") != 1:
        raise ValueError("Неизвестный формат состояния; очередь не сброшена")
    document.setdefault("messages", {})
    datetime.strptime(document["cursor"], "%Y-%m-%dT%H:%M:%SZ")
    for name in ("seen", "pending", "commit_comments", "messages"):
        if not isinstance(document.get(name), dict):
            raise ValueError(f"Повреждено поле состояния {name}")
    for key, message_ids in document["messages"].items():
        if isinstance(message_ids, str):
            message_ids = document["messages"][key] = [message_ids]
        if not isinstance(message_ids, list) or any(
            message_id is not None
            and (
                not isinstance(message_id, str)
                or not message_id.isdecimal()
                or len(message_id) > 30
            )
            for message_id in message_ids
        ):
            raise ValueError("Повреждены ID сообщений Discord")


class State:
    def __init__(self, github: GitHub, path: Path) -> None:
        self.github = github
        self.path = path
        self.data = json.loads(path.read_text(encoding="utf-8"))
        validate(self.data)
        self.saved = self.serialize()

    def serialize(self) -> str:
        return json.dumps(self.data, ensure_ascii=False, separators=(",", ":"))

    def save(self) -> None:
        serialized = self.serialize()
        if serialized == self.saved:
            return
        write_state(self.path, serialized)
        self.saved = serialized


def write_state(path: Path, serialized: str) -> None:
    if len(serialized.encode("utf-8")) > 26 * 1024 * 1024:
        raise ValueError("Снимок очереди превышает 26 МБ; запись остановлена")
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".tmp")
    temporary.write_text(serialized, encoding="utf-8")
    temporary.replace(path)


def snapshots(github: GitHub, current: dict) -> list[dict]:
    result = []
    default_branch = github.json("GET", github.root)["default_branch"]
    for name in ARTIFACTS:
        for artifact in github.pages(
            f"{github.root}/actions/artifacts", "artifacts", name=name
        ):
            source = artifact.get("workflow_run") or {}
            if (
                artifact["name"] != name
                or source.get("head_branch") != default_branch
            ):
                continue
            run = github.json(
                "GET", f"{github.root}/actions/runs/{source['id']}"
            )
            if (
                run["workflow_id"] == current["workflow_id"]
                and run["head_repository"]["full_name"].casefold()
                == github.repository.casefold()
                and run["event"]
                in {"schedule", "workflow_run", "workflow_dispatch"}
            ):
                result.append(artifact)
    return sorted(result, key=lambda item: item["id"], reverse=True)


def restore(github: GitHub, current: dict, path: Path, hours: int) -> None:
    available = snapshots(github, current)
    if available:
        latest = available[0]
        if latest["expired"]:
            raise GitHubError(
                "Срок хранения последнего снимка истёк. "
                "Нужна сохранённая копия очереди; пустая очередь не создана"
            )
        document = github.artifact_json(latest["id"], "discord-state.json")
        validate(document)
        checkpoint = document["checkpoint"]
        if checkpoint["run_id"] != latest["workflow_run"]["id"]:
            raise GitHubError("Снимок очереди принадлежит другому запуску")
        print(
            f"Очередь восстановлена из артефакта {latest['id']}. "
            f"Ожидают доставки: {len(document['pending'])}."
        )
        if latest["name"] == ARTIFACTS[0]:
            print(
                "Использован снимок до отправки: итоговый снимок отсутствует. "
                "После аварийного обрыва возможен повтор части сообщений."
            )
    else:
        runs = github.pages(
            f"{github.root}/actions/workflows/{current['workflow_id']}/runs",
            "workflow_runs",
        )
        for run in runs:
            attempts = range(1, run["run_attempt"] + 1)
            if run["id"] == current["id"]:
                attempts = range(1, current["run_attempt"])
            for attempt in attempts:
                jobs = github.pages(
                    f"{github.root}/actions/runs/{run['id']}/"
                    f"attempts/{attempt}/jobs",
                    "jobs",
                )
                if any(
                    step["name"] == SEND_STEP
                    and step["conclusion"] != "skipped"
                    for job in jobs
                    for step in job.get("steps", [])
                ):
                    raise GitHubError(
                        "Артефакты очереди потеряны, "
                        "но отправка уже запускалась. "
                        "Автоматический сброс запрещён; восстановите снимок"
                    )
        document = {
            "version": 1,
            "cursor": (datetime.now(UTC) - timedelta(hours=hours)).strftime(
                "%Y-%m-%dT%H:%M:%SZ"
            ),
            "seen": {},
            "pending": {},
            "commit_comments": {},
            "messages": {},
        }
        print("Первое включение: создана пустая очередь уведомлений.")
    document["checkpoint"] = {
        "run_id": current["id"],
        "run_attempt": current["run_attempt"],
    }
    write_state(path, json.dumps(document, ensure_ascii=False))


def prune(github: GitHub, current: dict, uploaded_id: int) -> None:
    available = snapshots(github, current)
    if not available or not any(
        item["name"] == ARTIFACTS[1]
        and item["id"] == uploaded_id
        and item["workflow_run"]["id"] == current["id"]
        and not item["expired"]
        for item in available
    ):
        raise GitHubError("Новый итоговый снимок не найден; старые не удалены")
    for name in ARTIFACTS:
        older = [item for item in available if item["name"] == name][3:]
        for artifact in older:
            github.json(
                "DELETE", f"{github.root}/actions/artifacts/{artifact['id']}"
            )
    print("Сохранены три последних снимка каждого вида; старые удалены.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=("restore", "prune"))
    operation = parser.parse_args().operation
    try:
        github = GitHub(
            os.environ["GITHUB_REPOSITORY"], os.environ["GITHUB_TOKEN"]
        )
        run_id = int(os.environ["GITHUB_RUN_ID"])
        current = github.json("GET", f"{github.root}/actions/runs/{run_id}")
        if operation == "restore":
            config = load_config(Path(__file__).with_name("config.toml"))
            restore(
                github,
                current,
                Path(os.environ["DISCORD_STATE_PATH"]),
                config["delivery"]["bootstrap_hours"],
            )
        else:
            prune(
                github, current, int(os.environ["DISCORD_STATE_ARTIFACT_ID"])
            )
        return 0
    except Exception as error:
        print(f"::error::Операция с очередью остановлена: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
