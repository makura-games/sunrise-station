"""Отправка JSON и вложений Discord с ограниченными повторами."""

import base64
import copy
import json
import math
import re
import time
from collections.abc import Callable
from urllib.parse import parse_qsl, urlencode, urlsplit, urlunsplit

import requests


class DiscordError(requests.HTTPError):
    """Безопасная для журнала ошибка без закрытого адреса вебхука."""

    def __init__(self, status_code: int, reason: str) -> None:
        self.status_code = status_code
        super().__init__(f"Discord: {reason} (HTTP {status_code})")


class UnexpectedDiscordStatusError(RuntimeError):
    def __init__(self, status_code: int) -> None:
        self.status_code = status_code
        super().__init__(
            f"Discord webhook вернул неожиданный статус {status_code}"
        )


class DiscordPublishTimeoutError(TimeoutError):
    def __init__(self) -> None:
        super().__init__("Истекло время отправки сообщения в Discord")


def retry_after(response: requests.Response, default: float = 1) -> float:
    """Читает задержку Discord; некорректный ответ не ломает повторы."""
    try:
        delay = response.json().get("retry_after")
    except (ValueError, AttributeError, TypeError):
        delay = None
    if delay is None:
        try:
            delay = float(response.headers.get("Retry-After", default))
        except (ValueError, TypeError):
            return default
    if isinstance(delay, bool) or not isinstance(delay, (int, float)):
        return default
    if isinstance(delay, float) and not math.isfinite(delay):
        return default
    return delay if delay >= 0 else default


def webhook_url(address: str) -> str:
    """Проверяет получателя и включает подтверждение сохранения сообщения."""
    if not isinstance(address, str):
        raise ValueError("Не задан секрет с адресом Discord")
    parsed = urlsplit(address)
    if (
        parsed.scheme != "https"
        or parsed.hostname not in {"discord.com", "discordapp.com"}
        or parsed.username
        or parsed.password
        or parsed.port not in {None, 443}
        or parsed.fragment
        or not re.fullmatch(
            r"/api(?:/v\d+)?/webhooks/\d+/[A-Za-z0-9._-]+(?:/github)?/?",
            parsed.path,
        )
    ):
        raise ValueError("Получатель должен быть HTTPS-вебхуком Discord")
    path = parsed.path.rstrip("/").removesuffix("/github")
    query = dict(parse_qsl(parsed.query))
    query["wait"] = "true"
    return urlunsplit(
        (parsed.scheme, parsed.netloc, path, urlencode(query), "")
    )


def send_message(
    address: str,
    payload: dict,
    *,
    message_id: str | None = None,
    files: list | None = None,
    delete: bool = False,
    attempts: int = 6,
    timeout: float = 30,
    deadline: float | None = None,
    report: Callable[[str], None] = print,
) -> dict:
    """Повторяет временные отказы, не печатая URL, ответы или секреты."""
    address = webhook_url(address)
    message = copy.deepcopy(payload)
    if delete and message_id is None:
        raise ValueError("Для удаления нужен ID сообщения Discord")
    if message_id is not None:
        if not re.fullmatch(r"\d{1,30}", message_id):
            raise ValueError("Некорректный ID сообщения Discord")
        parsed = urlsplit(address)
        address = urlunsplit(
            (
                parsed.scheme,
                parsed.netloc,
                f"{parsed.path}/messages/{message_id}",
                parsed.query,
                "",
            )
        )
        message.pop("username", None)
        message.pop("avatar_url", None)
    encoded_files = message.pop("_files", {})
    if encoded_files:
        files = list(files or [])
        for name, encoded in encoded_files.items():
            if not re.fullmatch(r"[a-zA-Z0-9_-]{1,64}\.png", name):
                raise ValueError("Некорректное имя вложения")
            if not isinstance(encoded, str) or len(encoded) > 8_000_000:
                raise ValueError("Вложение слишком велико")
            data = base64.b64decode(encoded, validate=True)
            if not data.startswith(b"\x89PNG\r\n\x1a\n"):
                raise ValueError("Ожидалось вложение PNG")
            files.append((f"files[{len(files)}]", (name, data, "image/png")))
    if message_id is not None and not delete:
        attachments = message.setdefault("attachments", [])
        if not isinstance(attachments, list):
            raise ValueError("Поле attachments должно быть списком")
        for file in files or []:
            try:
                field, body = file
                match = re.fullmatch(r"files\[(\d+)]", field)
                filename = body[0]
            except (TypeError, ValueError, IndexError):
                raise ValueError("Некорректное вложение Discord") from None
            if not match or not isinstance(filename, str) or not filename:
                raise ValueError("Некорректное вложение Discord")
            attachments.append(
                {"id": int(match.group(1)), "filename": filename}
            )
    message.setdefault("allowed_mentions", {"parse": []})
    if message.get("flags", 0) & (1 << 15):
        address += "&with_components=true"
    if deadline is None:
        deadline = time.monotonic() + 240
    reasons = {
        400: "неверный формат или превышен размер сообщения",
        401: "неверный ключ вебхука",
        403: "отправка запрещена или нет доступа к каналу",
        404: "вебхук или канал удалён",
        413: "слишком большое сообщение или вложение",
        429: "слишком частая отправка; Discord просит подождать",
    }
    for attempt in range(1, attempts + 1):
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise DiscordPublishTimeoutError
        delay = min(2 ** (attempt - 1), 30)
        error = DiscordError(0, "сеть недоступна или истекло время ответа")
        try:
            options = {
                "timeout": min(timeout, remaining),
                "allow_redirects": False,
            }
            if delete:
                response = requests.delete(address, **options)
            elif not files:
                request = requests.patch if message_id else requests.post
                response = request(address, json=message, **options)
            else:
                request = requests.patch if message_id else requests.post
                response = request(
                    address,
                    data={
                        "payload_json": json.dumps(message, ensure_ascii=False)
                    },
                    files=files,
                    **options,
                )
            status = response.status_code
            if delete and status == 404:
                report("Сообщение Discord уже отсутствует.")
                return {"id": message_id}
            if status in {200, 204}:
                confirmation = (
                    "Удаление подтверждено"
                    if delete
                    else "Изменение подтверждено"
                    if message_id
                    else "Доставка подтверждена"
                )
                report(f"{confirmation} Discord: HTTP {status}.")
                try:
                    result = response.json()
                except ValueError:
                    result = {}
                if delete:
                    return {"id": message_id}
                return result if isinstance(result, dict) else {}
            if 200 <= status < 400:
                raise UnexpectedDiscordStatusError(status)
            reason = reasons.get(status, "временная ошибка сервера Discord")
            error = DiscordError(status, reason)
            if status != 429 and not 500 <= status <= 599:
                raise error
            if status == 429:
                delay = retry_after(response)
        except (requests.ConnectionError, requests.Timeout):
            pass
        report(f"Попытка {attempt}/{attempts} не удалась: {error}.")
        if attempt == attempts or delay >= deadline - time.monotonic():
            raise error
        report(f"Повторная попытка через {delay} с.")
        time.sleep(delay)
    raise DiscordPublishTimeoutError
