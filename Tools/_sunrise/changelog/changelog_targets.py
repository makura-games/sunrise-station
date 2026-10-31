#!/usr/bin/env python3
import argparse
import os
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path

import yaml

from changelog_path import validate_changelog_path


TARGETS_PATH = Path("Tools/_sunrise/changelog/targets")
TARGET_FIELDS = {"changelog_file", "webhook_secret"}


@dataclass(frozen=True)
class ChangelogTarget:
    target_id: str
    changelog_file: Path
    webhook_secret: str


def validate_target_id(value: str | None) -> str:
    if not isinstance(value, str) or not value or "\n" in value or "\r" in value:
        raise RuntimeError("Идентификатор цели не может быть пустым или содержать перенос строки")
    value = value.strip()
    if not value:
        raise RuntimeError("Идентификатор цели не может состоять только из пробелов")
    return value


def target_paths(repo_root: Path) -> list[Path]:
    targets_path = repo_root / TARGETS_PATH
    if not targets_path.is_dir():
        raise RuntimeError(f"Каталог целей чейнджлога не найден: {TARGETS_PATH}")

    candidates = sorted(targets_path.glob("*.yml"), key=lambda path: path.name)
    if not candidates:
        raise RuntimeError(f"В каталоге {TARGETS_PATH} нет целей чейнджлога")

    paths: list[Path] = []
    for path in candidates:
        try:
            if yaml.safe_load(path.read_text(encoding="utf-8-sig")) is None:
                print(f"Пропущена пустая цель {path.name}")
                continue
        except yaml.YAMLError:
            # Ошибка будет привязана к конкретной цели при полной загрузке.
            pass
        paths.append(path)
    return paths


def load_target(path: Path, repo_root: Path) -> ChangelogTarget:
    target_id = validate_target_id(path.stem)
    document = yaml.safe_load(path.read_text(encoding="utf-8-sig"))
    if not isinstance(document, Mapping):
        raise RuntimeError(f"Цель {target_id} должна быть YAML-отображением")
    if not all(isinstance(field, str) for field in document):
        raise RuntimeError(f"Цель {target_id} содержит некорректное имя поля")

    unknown_fields = set(document) - TARGET_FIELDS
    if unknown_fields:
        raise RuntimeError(
            f"Цель {target_id} содержит неизвестные поля: {', '.join(sorted(unknown_fields))}",
        )

    changelog_value = document.get("changelog_file")
    if not isinstance(changelog_value, str):
        raise RuntimeError(f"Цель {target_id} должна содержать строковое поле changelog_file")
    changelog_file = validate_changelog_path(changelog_value)
    if not (repo_root / changelog_file).is_file():
        raise RuntimeError(f"Файл цели {target_id} не найден: {changelog_file}")

    webhook_secret = document.get("webhook_secret")
    if (
        not isinstance(webhook_secret, str)
        or not webhook_secret
        or "\n" in webhook_secret
        or "\r" in webhook_secret
    ):
        raise RuntimeError(f"Цель {target_id} должна содержать имя секрета в поле webhook_secret")

    return ChangelogTarget(target_id, changelog_file, webhook_secret)


def resolve_target(repo_root: Path, target_id: str) -> ChangelogTarget:
    target_id = validate_target_id(target_id)
    for target_path in target_paths(repo_root):
        if target_path.stem == target_id:
            return load_target(target_path, repo_root)
    raise RuntimeError(f"Цель чейнджлога не найдена: {target_id}")


def write_github_outputs(target: ChangelogTarget) -> None:
    output_path = os.environ.get("GITHUB_OUTPUT")
    if not output_path:
        raise RuntimeError("Переменная GITHUB_OUTPUT не задана")

    changelog_file = target.changelog_file.as_posix()
    if "\n" in changelog_file or "\r" in changelog_file:
        raise RuntimeError("Путь к чейнджлогу не может содержать перенос строки")

    with open(output_path, "a", encoding="utf-8") as output:
        output.write(f"target_id={target.target_id}\n")
        output.write(f"changelog_file={changelog_file}\n")
        output.write(f"webhook_secret={target.webhook_secret}\n")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("target_id")
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[3]
    write_github_outputs(resolve_target(repo_root, args.target_id))


if __name__ == "__main__":
    main()
