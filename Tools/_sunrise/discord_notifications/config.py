"""Загрузка проверяемых пользовательских правил TOML."""

import re
import tomllib
from pathlib import Path


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
        "max_pending_messages": (1, 10000),
        "retry_interval": (1, 3600),
        "max_retry_interval": (1, 86400),
    }
    for name, (minimum, maximum) in limits.items():
        value = config["delivery"][name]
        if type(value) is not int or not minimum <= value <= maximum:
            raise ValueError(f"delivery.{name}: требуется {minimum}–{maximum}")
    for name, maximum in (
        ("body_length", 3500),
        ("max_commits", 20),
        ("commit_length", 200),
    ):
        value = config["display"][name]
        if type(value) is not int or not 1 <= value <= maximum:
            raise ValueError(f"display.{name}: требуется 1–{maximum}")
    if config["display"]["body_length"] < 128:
        raise ValueError("display.body_length: требуется не менее 128")
    for name, style in config["styles"].items():
        if style["color"] != "" and not re.fullmatch(
            r"#[0-9a-fA-F]{6}", style["color"]
        ):
            raise ValueError(f"styles.{name}.color: требуется #RRGGBB или ''")
        if "icon" in style and style["icon"] not in config["icons"]:
            raise ValueError(
                f"styles.{name}.icon: требуется имя из раздела icons"
            )
    for section in ("text", "pr_status", "review_status", "icons"):
        for name, value in config[section].items():
            if not isinstance(value, str) or len(value) > 100:
                raise ValueError(
                    f"{section}.{name}: требуется короткая строка"
                )
    for section in config["reviews"].values():
        for name, value in section.items():
            if name.endswith("color") and not re.fullmatch(
                r"#[0-9a-fA-F]{6}", value
            ):
                raise ValueError(f"{name}: требуется #RRGGBB")
            if name == "icon" and value not in config["icons"]:
                raise ValueError(
                    "reviews.icon: требуется имя из раздела icons"
                )
    if type(config["display"]["show_reactions"]) is not bool:
        raise ValueError("display.show_reactions: требуется true или false")
    if type(config["filters"]["ignore_bots"]) is not bool:
        raise ValueError("filters.ignore_bots: требуется true или false")
    lists = [*config["events"].values()]
    lists += [
        config["filters"][name]
        for name in ("ignored_users", "branches", "ignored_labels")
    ]
    if any(
        not isinstance(items, list)
        or any(not isinstance(item, str) for item in items)
        for items in lists
    ):
        raise ValueError("Фильтры событий должны быть списками строк")
    return config
