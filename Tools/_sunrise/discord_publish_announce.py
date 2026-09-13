#!/usr/bin/env python3

import os
import subprocess

from discord_notifications.transport import send_message

DISCORD_WEBHOOK_URL = os.environ.get("DISCORD_WEBHOOK_URL")
DISCORD_ROLE_ID = os.environ.get("DISCORD_ROLE_ID")
BUILD_PATH = os.environ.get("BUILD_PATH")
VERSION = subprocess.check_output(["git", "rev-parse", "HEAD"], encoding="UTF-8").strip()

def get_build_size(path):
    try:
        size = os.path.getsize(path)
        for unit in ['Б', 'КБ', 'МБ', 'ГБ']:
            if size < 1024:
                return f"{size:.2f} {unit}"
            size /= 1024
        return f"{size:.2f} ТБ"
    except Exception:
        return "Не удалось определить"

def get_engine_version() -> str:
    proc = subprocess.run(["git", "describe","--tags", "--abbrev=0"], stdout=subprocess.PIPE, cwd="RobustToolbox", check=True, encoding="UTF-8")
    tag = proc.stdout.strip()
    assert tag.startswith("v")
    return tag[1:] # Cut off v prefix.

def main():
    if not DISCORD_WEBHOOK_URL or not DISCORD_ROLE_ID:
        print("Не указаны DISCORD_WEBHOOK_URL или DISCORD_ROLE_ID")
        return

    build_size = get_build_size(BUILD_PATH) if BUILD_PATH else "Не указано"
    engine_version = get_engine_version() or "Не указано"
    build_hash = VERSION or "Не указано"

    content = f"<@&{DISCORD_ROLE_ID}> Сервер был обновлен!\n"
    embed = {
        "title": "Обновление сервера",
        "fields": [
            {"name": "Версия движка", "value": engine_version, "inline": True},
            {"name": "Размер билда", "value": build_size, "inline": True},
            {"name": "Версия", "value": build_hash, "inline": False},
        ],
        "color": 0x2ecc71
    }

    payload = {
        "content": content,
        "embeds": [embed],
        "allowed_mentions": {"roles": [DISCORD_ROLE_ID]}
    }

    send_message(DISCORD_WEBHOOK_URL, payload)
    print(f"Отправлено объявление об обновлении сервера: версия {build_hash}.")

if __name__ == "__main__":
    main()
