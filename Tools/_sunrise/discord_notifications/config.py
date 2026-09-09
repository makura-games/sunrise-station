"""Загрузка проверяемых пользовательских правил TOML."""

import re
from pathlib import Path

import tomllib


def load_config(path: Path) -> dict:
    with path.open("rb") as stream:
        config = tomllib.load(stream)
    limits = {
        "attempts": (1, 20),
        "request_timeout": (1, 60),
        "message_timeout": (1, 600),
        "run_timeout": (1, 600),
        "bootstrap_hours": (1, 2160),
        "max_messages_per_run": (1, 200),
        "retry_interval": (1, 3600),
        "max_retry_interval": (1, 86400),
    }
    for name, (minimum, maximum) in limits.items():
        value = config["delivery"][name]
        if type(value) is not int or not minimum <= value <= maximum:
            raise ValueError(f"delivery.{name}: требуется {minimum}–{maximum}")
    branch = config["delivery"]["state_branch"]
    if not re.fullmatch(r"[a-z][a-z0-9-]{2,80}", branch):
        raise ValueError("Некорректное имя служебной ветки")
    for name, maximum in (("body_length", 2000), ("max_commits", 20)):
        value = config["display"][name]
        if type(value) is not int or not 1 <= value <= maximum:
            raise ValueError(f"display.{name}: требуется 1–{maximum}")
    for name, style in config["styles"].items():
        if not re.fullmatch(r"#[0-9a-fA-F]{6}", style["color"]):
            raise ValueError(f"styles.{name}.color: требуется #RRGGBB")
        for field in ("emoji", "label"):
            if not isinstance(style[field], str) or len(style[field]) > 100:
                raise ValueError(
                    f"styles.{name}.{field}: слишком длинный текст"
                )
    for name, destination in config["destinations"].items():
        if not re.fullmatch(r"[A-Z][A-Z0-9_]+", destination["webhook_env"]):
            raise ValueError(f"destinations.{name}: неверное имя секрета")
    lists = [*config["events"].values()]
    lists += [
        config["filters"][name]
        for name in ("ignored_users", "branches", "ignored_labels")
    ]
    lists += [
        item["excluded_events"] for item in config["destinations"].values()
    ]
    if any(
        not isinstance(items, list)
        or any(not isinstance(item, str) for item in items)
        for items in lists
    ):
        raise ValueError("Фильтры событий должны быть списками строк")
    return config
