#!/usr/bin/env python3

import json
from collections.abc import Mapping, MutableMapping
from dataclasses import dataclass
from typing import Any


FIXABLE_CODES = {"missing_id", "duplicate_id", "nested_id"}


@dataclass(frozen=True)
class ChangelogIssue:
    code: str
    message: str
    suggestion: str

    @property
    def fixable(self) -> bool:
        return self.code in FIXABLE_CODES


@dataclass(frozen=True)
class ChangelogRepair:
    issue: ChangelogIssue
    resolution: str


class ChangelogSchemaError(ValueError):
    def __init__(self, issues: list[ChangelogIssue]):
        self.issues = issues
        super().__init__("; ".join(issue.message for issue in issues))


def format_issue_for_user(issue: ChangelogIssue) -> str:
    return f"Проблема: {issue.message} Как исправить: {issue.suggestion}"


def _entry_label(index: int) -> str:
    return f"Запись #{index + 1}"


def inspect_changelog_document(document: Any) -> list[ChangelogIssue]:
    if not isinstance(document, Mapping):
        return [
            ChangelogIssue(
                "document",
                "Корень чейнжлога должен быть YAML-отображением.",
                "Исправьте структуру файла вручную.",
            ),
        ]

    entries = document.get("Entries")
    if not isinstance(entries, list):
        return [
            ChangelogIssue(
                "entries",
                "Поле Entries должно содержать список записей.",
                "Исправьте структуру файла вручную.",
            ),
        ]

    issues: list[ChangelogIssue] = []
    seen_ids: set[int] = set()
    for entry_index, entry in enumerate(entries):
        label = _entry_label(entry_index)
        if not isinstance(entry, Mapping):
            issues.append(
                ChangelogIssue(
                    "entry",
                    f"{label}: значение должно быть YAML-отображением.",
                    "Исправьте структуру записи вручную.",
                ),
            )
            continue

        author = entry.get("author")
        if not isinstance(author, str) or not author.strip():
            issues.append(
                ChangelogIssue(
                    "author",
                    f"{label}: поле author должно быть непустой строкой.",
                    "Исправьте автора записи вручную.",
                ),
            )

        timestamp = entry.get("time")
        if not isinstance(timestamp, str) or not timestamp.strip():
            issues.append(
                ChangelogIssue(
                    "time",
                    f"{label}: поле time должно быть непустой строкой.",
                    "Исправьте время записи вручную.",
                ),
            )

        changes = entry.get("changes")
        if not isinstance(changes, list) or not changes:
            issues.append(
                ChangelogIssue(
                    "changes",
                    f"{label}: поле changes должно содержать непустой список изменений.",
                    "Исправьте список изменений вручную.",
                ),
            )
        else:
            for change_index, change in enumerate(changes):
                change_label = f"{label}, изменение #{change_index + 1}"
                if not isinstance(change, Mapping):
                    issues.append(
                        ChangelogIssue(
                            "change",
                            f"{change_label}: значение должно быть YAML-отображением.",
                            "Исправьте изменение вручную.",
                        ),
                    )
                    continue

                if "id" in change:
                    issues.append(
                        ChangelogIssue(
                            "nested_id",
                            f"{change_label}: поле id находится внутри changes; id должен быть полем записи.",
                            "Перенесите id на уровень записи, рядом с author, time и changes.",
                        ),
                    )

                change_type = change.get("type")
                if not isinstance(change_type, str) or not change_type.strip():
                    issues.append(
                        ChangelogIssue(
                            "change_type",
                            f"{change_label}: поле type должно быть непустой строкой.",
                            "Исправьте тип изменения вручную.",
                        ),
                    )

                message = change.get("message")
                if not isinstance(message, str):
                    issues.append(
                        ChangelogIssue(
                            "message",
                            f"{change_label}: поле message должно быть строкой.",
                            "Исправьте текст изменения вручную.",
                        ),
                    )

        entry_id = entry.get("id")
        if type(entry_id) is not int or entry_id <= 0:
            issues.append(
                ChangelogIssue(
                    "missing_id",
                    f"{label}: отсутствует положительный целочисленный верхнеуровневый id.",
                    "Добавьте записи новый уникальный положительный целочисленный id.",
                ),
            )
        elif entry_id in seen_ids:
            issues.append(
                ChangelogIssue(
                    "duplicate_id",
                    f"{label}: верхнеуровневый id {entry_id} уже используется другой записью.",
                    "Замените повторяющийся id на новый уникальный номер.",
                ),
            )
        else:
            seen_ids.add(entry_id)

    return issues


def repair_changelog_document(document: Any) -> list[ChangelogRepair]:
    issues = inspect_changelog_document(document)
    fatal = [issue for issue in issues if not issue.fixable]
    if fatal:
        raise ChangelogSchemaError(fatal)

    entries = document["Entries"]
    max_id = max(
        (
            entry["id"]
            for entry in entries
            if isinstance(entry, Mapping)
            and type(entry.get("id")) is int
            and entry["id"] > 0
        ),
        default=0,
    )
    repairs: list[ChangelogRepair] = []
    seen_ids: set[int] = set()

    for entry_index, entry in enumerate(entries):
        assert isinstance(entry, MutableMapping)
        label = _entry_label(entry_index)

        changes = entry["changes"]
        for change_index, change in enumerate(changes):
            assert isinstance(change, MutableMapping)
            if "id" not in change:
                continue

            issue = ChangelogIssue(
                "nested_id",
                f"{label}, изменение #{change_index + 1}: поле id находится внутри changes; "
                "id должен быть полем записи.",
                "Перенесите id на уровень записи, рядом с author, time и changes.",
            )
            del change["id"]
            repairs.append(ChangelogRepair(issue, "Вложенное поле id удалено."))

        entry_id = entry.get("id")
        if type(entry_id) is int and entry_id > 0 and entry_id not in seen_ids:
            seen_ids.add(entry_id)
            continue

        max_id += 1
        if type(entry_id) is int and entry_id > 0:
            issue = ChangelogIssue(
                "duplicate_id",
                f"{label}: верхнеуровневый id {entry_id} уже используется другой записью.",
                "Замените повторяющийся id на новый уникальный номер.",
            )
            resolution = f"Повторяющийся id {entry_id} заменён на {max_id}."
        else:
            issue = ChangelogIssue(
                "missing_id",
                f"{label}: отсутствует положительный целочисленный верхнеуровневый id.",
                "Добавьте записи новый уникальный положительный целочисленный id.",
            )
            resolution = f"Назначен новый верхнеуровневый id {max_id}."

        entry["id"] = max_id
        seen_ids.add(max_id)
        repairs.append(ChangelogRepair(issue, resolution))

    remaining = inspect_changelog_document(document)
    if remaining:
        raise ChangelogSchemaError(remaining)
    return repairs


def changelog_entry_identity(entry: Mapping[str, Any]) -> tuple[str, str]:
    url = entry.get("url")
    if isinstance(url, str) and url:
        return "url", url

    def normalize(value: Any) -> Any:
        if isinstance(value, Mapping):
            return {
                key: normalize(item)
                for key, item in value.items()
                if key not in {"id", "time"}
            }
        if isinstance(value, list):
            return [normalize(item) for item in value]
        return value

    return "content", json.dumps(normalize(entry), ensure_ascii=False, sort_keys=True)
