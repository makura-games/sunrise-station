#!/usr/bin/env python3
#
# Отправляет новые записи чейнджлога в вебхук Discord после последнего запуска публикации GitHub Actions.
# Автоматически определяет последний запуск и получает чейнджлог через GitHub API.
#
import http.client
import ipaddress
import os
import re
import socket
import ssl
import sys
import time
import textwrap
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from urllib.parse import quote, urljoin, urlsplit, urlunsplit

import requests
import yaml
from typing import Any, Iterable

from changelog_path import validate_changelog_path
from changelog_schema import changelog_entry_identity
from changelog_targets import validate_target_id
from dispatch_changelogs import published_run_sha

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from discord_notifications.transport import (
    DiscordPublishTimeoutError,
    UnexpectedDiscordStatusError,
    send_message,
)

DEBUG = False
DEBUG_CHANGELOG_FILE_OLD = Path("Resources/Changelog/Old.yml")
GITHUB_API_URL    = os.environ.get("GITHUB_API_URL", "https://api.github.com")
HTTP_REQUEST_TIMEOUT = 30
DISCORD_RETRY_LIMIT = 5
DISCORD_PUBLISH_TIMEOUT = 14 * 60
DISCORD_COMPONENTS_V2_FLAG = 1 << 15
MEDIA_MAX_SIZE = 10 * 1024 * 1024
MEDIA_MAX_FILES_PER_REQUEST = 10
MEDIA_MAX_FILES_PER_ENTRY = 20
MEDIA_MAX_REQUEST_SIZE = 24 * 1024 * 1024
MEDIA_MAX_REDIRECTS = 3
MEDIA_READ_CHUNK_SIZE = 64 * 1024
MEDIA_REDIRECT_STATUSES = {300, 301, 302, 303, 307, 308}
DISCORD_RUN_TITLE_RE = re.compile(
    r"^Discord changelog (?P<target>.+) for (?P<sha>[0-9a-f]{40,64})$",
)
SHA_RE = re.compile(r"^[0-9a-f]{40,64}$")

# https://discord.com/developers/docs/resources/webhook
DISCORD_SPLIT_LIMIT = 2000
DISCORD_WEBHOOK_URL = os.environ.get("DISCORD_WEBHOOK_URL")

CHANGELOG_FILE = os.environ.get("CHANGELOG_FILE")

CHANGE_GROUPS = (
    ("Add", "💡", "Добавлено"),
    ("Tweak", "⚒️", "Изменено"),
    ("Fix", "🪛", "Исправлено"),
    ("Remove", "❌", "Удалено"),
)

ChangelogEntry = dict[str, Any]


def grouped_changes(
    changes: list[Any],
) -> list[tuple[str, str, list[tuple[int, Mapping[str, Any]]]]]:
    groups = []
    valid_changes = [
        (index, change)
        for index, change in enumerate(changes)
        if isinstance(change, Mapping)
        and isinstance(change.get("type"), str)
        and change["type"].strip()
        and isinstance(change.get("message"), str)
    ]
    known_types = {change_type for change_type, _, _ in CHANGE_GROUPS}
    for change_type, emoji, title in CHANGE_GROUPS:
        items = [(index, change) for index, change in valid_changes if change["type"] == change_type]
        if items:
            groups.append((emoji, title, items))

    other = [(index, change) for index, change in valid_changes if change["type"] not in known_types]
    if other:
        groups.append(("❓", "Прочее", other))
    return groups


def format_grouped_change_parts(
    changes: list[Any],
    url: str | None = None,
    limit: int = DISCORD_SPLIT_LIMIT,
) -> list[str]:
    sections: list[tuple[str, bool]] = []
    for emoji, title, items in grouped_changes(changes):
        header = f"{emoji} **{title}**"
        body = "\n".join(f"• {change['message']}" for _, change in items)
        for index, part in enumerate(split_message(body, limit - len(header) - 1)):
            sections.append((f"{header}\n{part}", index > 0))

    parts: list[str] = []
    for section, continues_group in sections:
        if not continues_group and parts and len(parts[-1]) + len(section) + 2 <= limit:
            parts[-1] += f"\n\n{section}"
        else:
            parts.append(section)

    if url and url.strip():
        link = f"[GitHub Pull Request]({url})"
        if parts and len(parts[-1]) + len(link) + 2 <= limit:
            parts[-1] += f"\n\n{link}"
        else:
            parts.append(link)
    return parts


@dataclass(frozen=True)
class DownloadedMedia:
    url: str
    description: str | None
    data: bytes
    content_type: str
    filename: str
    change_index: int | None = None


@dataclass(frozen=True)
class RemoteMedia:
    url: str
    description: str | None
    change_index: int | None = None


DiscordMedia = DownloadedMedia | RemoteMedia


class MediaError(ValueError):
    pass


class UnsafeMediaUrlError(MediaError):
    pass


def validate_media_url(url: str):
    if not isinstance(url, str) or not url or url != url.strip() or any(char.isspace() for char in url):
        raise UnsafeMediaUrlError("ссылка должна быть непустым URL без пробелов")

    try:
        parsed = urlsplit(url)
        hostname = parsed.hostname
        port = parsed.port
    except ValueError as error:
        raise UnsafeMediaUrlError(f"некорректный URL: {error}") from error

    if parsed.scheme.casefold() != "https":
        raise UnsafeMediaUrlError("разрешены только HTTPS-ссылки")
    if parsed.username is not None or parsed.password is not None:
        raise UnsafeMediaUrlError("ссылки с credentials запрещены")
    if hostname is None:
        raise UnsafeMediaUrlError("в URL отсутствует имя хоста")
    if port not in (None, 443):
        raise UnsafeMediaUrlError("разрешён только стандартный HTTPS-порт 443")

    return parsed


def _is_public_ip(address: str) -> bool:
    try:
        parsed = ipaddress.ip_address(address)
    except ValueError:
        return False
    return parsed.is_global and not (
        parsed.is_private
        or parsed.is_loopback
        or parsed.is_link_local
        or parsed.is_reserved
        or parsed.is_multicast
        or parsed.is_unspecified
    )


def _resolve_public_address(hostname: str, port: int) -> tuple[int, str]:
    try:
        literal = ipaddress.ip_address(hostname)
    except ValueError:
        literal = None

    if literal is not None:
        if not _is_public_ip(hostname):
            raise UnsafeMediaUrlError("имя хоста указывает на запрещённый IP-адрес")
        family = socket.AF_INET6 if literal.version == 6 else socket.AF_INET
        return family, hostname

    try:
        addresses = socket.getaddrinfo(hostname, port, type=socket.SOCK_STREAM)
    except OSError as error:
        raise MediaError(f"не удалось разрешить имя хоста: {error}") from error

    resolved: list[tuple[int, str]] = []
    for family, _, _, _, sockaddr in addresses:
        if family not in (socket.AF_INET, socket.AF_INET6):
            continue
        address = sockaddr[0]
        if not _is_public_ip(address):
            raise UnsafeMediaUrlError("DNS-имя указывает на запрещённый IP-адрес")
        resolved.append((family, address))

    if not resolved:
        raise MediaError("DNS-имя не вернуло IP-адрес")
    return resolved[0]


class _VerifiedHTTPSConnection(http.client.HTTPSConnection):
    def __init__(self, hostname: str, port: int, address: tuple[int, str], timeout: float) -> None:
        super().__init__(hostname, port=port, timeout=timeout, context=ssl.create_default_context())
        self._verified_family, self._verified_address = address

    def connect(self) -> None:
        sock = socket.socket(self._verified_family, socket.SOCK_STREAM)
        try:
            sock.settimeout(self.timeout)
            address = (self._verified_address, self.port, 0, 0) if self._verified_family == socket.AF_INET6 else (
                self._verified_address,
                self.port,
            )
            sock.connect(address)
            self.sock = self._context.wrap_socket(sock, server_hostname=self.host)
        except BaseException:
            sock.close()
            raise


def detect_media_type(data: bytes) -> tuple[str, str] | None:
    if data.startswith(b"\xff\xd8\xff"):
        return "image/jpeg", "jpg"
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return "image/png", "png"
    if data.startswith((b"GIF87a", b"GIF89a")):
        return "image/gif", "gif"
    if len(data) >= 12 and data[:4] == b"RIFF" and data[8:12] == b"WEBP":
        return "image/webp", "webp"
    if data.startswith(b"\x1a\x45\xdf\xa3"):
        return "video/webm", "webm"
    if len(data) >= 12 and data[4:8] == b"ftyp":
        brand = data[8:12]
        mp4_brands = {
            b"avc1", b"dash", b"iso2", b"iso5", b"isom", b"mp41", b"mp42", b"M4V ", b"MSNV",
        }
        if brand == b"qt  ":
            return "video/quicktime", "mov"
        if brand in mp4_brands or any(
            data[offset:offset + 4] in mp4_brands
            for offset in range(16, min(len(data) - 3, 128), 4)
        ):
            return "video/mp4", "mp4"
    return None


def _remaining_timeout(deadline: float) -> float:
    remaining = deadline - time.monotonic()
    if remaining <= 0:
        raise DiscordPublishTimeoutError
    return min(HTTP_REQUEST_TIMEOUT, remaining)


def _read_media_response(response, connection: http.client.HTTPSConnection, deadline: float) -> bytes:
    content_length = response.getheader("Content-Length")
    if content_length:
        try:
            content_length = int(content_length)
        except (TypeError, ValueError):
            content_length = None
        if content_length is not None and content_length > MEDIA_MAX_SIZE:
            raise MediaError("файл превышает лимит 10 МиБ")

    data = bytearray()
    while True:
        if connection.sock is not None:
            connection.sock.settimeout(_remaining_timeout(deadline))
        chunk = response.read(MEDIA_READ_CHUNK_SIZE)
        if not chunk:
            break
        data.extend(chunk)
        if len(data) > MEDIA_MAX_SIZE:
            raise MediaError("файл превышает лимит 10 МиБ")
    return bytes(data)


def download_media(
    url: str,
    description: str | None = None,
    filename_prefix: str | None = None,
    deadline: float | None = None,
) -> DownloadedMedia:
    original_url = url
    current_url = url
    if deadline is None:
        deadline = time.monotonic() + DISCORD_PUBLISH_TIMEOUT

    for redirect_count in range(MEDIA_MAX_REDIRECTS + 1):
        parsed = validate_media_url(current_url)
        hostname = parsed.hostname.encode("idna").decode("ascii")
        port = parsed.port or 443
        address = _resolve_public_address(hostname, port)
        connection = _VerifiedHTTPSConnection(hostname, port, address, _remaining_timeout(deadline))

        try:
            request_path = urlunsplit(("", "", parsed.path or "/", parsed.query, ""))
            host_header = f"[{hostname}]" if ":" in hostname else hostname
            connection.request(
                "GET",
                request_path,
                headers={
                    "Accept-Encoding": "identity",
                    "Connection": "close",
                    "Host": host_header,
                },
            )
            response = connection.getresponse()
            if response.status in MEDIA_REDIRECT_STATUSES:
                location = response.getheader("Location")
                if not location:
                    raise MediaError("сервер вернул перенаправление без Location")
                if redirect_count >= MEDIA_MAX_REDIRECTS:
                    raise MediaError("превышено число перенаправлений")
                current_url = urljoin(current_url, location)
                continue
            if response.status != 200:
                raise MediaError(f"сервер вернул HTTP {response.status}")
            data = _read_media_response(response, connection, deadline)
        except DiscordPublishTimeoutError:
            raise
        except MediaError:
            raise
        except Exception as error:
            raise MediaError(f"ошибка загрузки: {error}") from error
        finally:
            connection.close()

        detected = detect_media_type(data)
        if detected is None:
            raise MediaError("сигнатура файла не поддерживается")
        content_type, extension = detected
        filename = f"{filename_prefix or 'media'}.{extension}"
        return DownloadedMedia(original_url, description, data, content_type, filename)

    raise MediaError("превышено число перенаправлений")


def report_media_warning(url: str, reason: str) -> None:
    message = f"Медиа {url}: {reason}"
    escaped = message.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
    print(f"::warning::{escaped}")


def require_environment(name: str) -> str:
    value = os.environ.get(name, "").strip()
    if not value:
        raise RuntimeError(f"Переменная {name} не задана")
    return value


def validate_runtime_environment() -> tuple[Path, str, str]:
    global CHANGELOG_FILE, DISCORD_WEBHOOK_URL

    DISCORD_WEBHOOK_URL = require_environment("DISCORD_WEBHOOK_URL")
    CHANGELOG_FILE = require_environment("CHANGELOG_FILE")
    target_id = validate_target_id(require_environment("CHANGELOG_TARGET_ID"))
    released_sha = require_environment("RELEASED_SHA")
    if not SHA_RE.fullmatch(released_sha):
        raise RuntimeError("Переменная RELEASED_SHA должна содержать SHA коммита")

    for name in (
        "GITHUB_REPOSITORY",
        "GITHUB_RUN_ID",
        "GITHUB_TOKEN",
        "SOURCE_WORKFLOW_RUN_ID",
    ):
        require_environment(name)

    return validate_changelog_path(CHANGELOG_FILE), target_id, released_sha


def main():
    changelog_file, _target_id, released_sha = validate_runtime_environment()

    if DEBUG:
        # Для локальной отладки можно использовать отдельный файл
        # в качестве предыдущего чейнджлога.
        last_changelog_stream = DEBUG_CHANGELOG_FILE_OLD.read_text()
        current_changelog_stream = changelog_file.read_text(encoding="utf-8-sig")
    else:
        # Обе версии загружаются через GitHub API, чтобы публикация всегда
        # использовала актуальный скрипт из основной ветки.
        last_changelog_stream = get_last_changelog(changelog_file)
        current_changelog_stream = get_released_changelog(changelog_file, released_sha)

    last_changelog = yaml.safe_load(last_changelog_stream)
    cur_changelog = yaml.safe_load(current_changelog_stream)

    diff = diff_changelog(last_changelog, cur_changelog)
    send_to_discord(diff)


def get_most_recent_workflow(
    sess: requests.Session,
    github_repository: str,
    github_run: str,
    target_id: str | None = None,
) -> Any | None:
    workflow_run = get_current_run(sess, github_repository, github_run)
    page = 1
    while True:
        past_runs = get_past_runs(sess, workflow_run, page)
        runs = past_runs["workflow_runs"]
        for run in runs:
            # Первый предыдущий успешный запуск, отличный от текущего.
            if run["id"] == workflow_run["id"]:
                continue

            if target_id is not None:
                title = str(run.get("display_title", ""))
                match = DISCORD_RUN_TITLE_RE.fullmatch(title)
                if match is None or match.group("target").strip() != target_id:
                    continue

            return run

        if len(runs) < 100:
            return None
        page += 1


def get_current_run(
    sess: requests.Session, github_repository: str, github_run: str
) -> Any:
    resp = sess.get(
        f"{GITHUB_API_URL}/repos/{github_repository}/actions/runs/{github_run}",
        timeout=HTTP_REQUEST_TIMEOUT,
    )
    resp.raise_for_status()
    return resp.json()


def get_past_runs(
    sess: requests.Session,
    current_run: Any,
    page: int = 1,
    status: str | None = "success",
) -> Any:
    """
    Возвращает запуски рабочего процесса с выбранным статусом до текущего.
    """
    params = {
        "created": f"<={current_run['created_at']}",
        "per_page": 100,
        "page": page,
    }
    if status is not None:
        params["status"] = status
    resp = sess.get(
        f"{current_run['workflow_url']}/runs",
        params=params,
        timeout=HTTP_REQUEST_TIMEOUT,
    )
    resp.raise_for_status()
    return resp.json()


def get_earliest_target_attempt(
    sess: requests.Session,
    github_repository: str,
    github_run: str,
    target_id: str,
) -> Any:
    current = get_current_run(sess, github_repository, github_run)
    attempts = [current]
    page = 1
    while True:
        response = get_past_runs(sess, current, page, "completed")
        runs = response["workflow_runs"]
        attempts.extend(run for run in runs if run.get("id") != current.get("id"))
        if len(runs) < 100:
            break
        page += 1

    matching = []
    for run in attempts:
        match = DISCORD_RUN_TITLE_RE.fullmatch(str(run.get("display_title", "")))
        if match is not None and match.group("target").strip() == target_id:
            matching.append(run)
    if not matching:
        raise RuntimeError(f"Не найден первый запуск публикации цели {target_id}")
    return min(matching, key=lambda run: run["created_at"])


def get_source_release_before_attempt(
    sess: requests.Session,
    github_repository: str,
    source_run_id: str,
    first_attempt: Any,
) -> Any:
    source_run = get_current_run(sess, github_repository, source_run_id)
    checkpoint = {
        "workflow_url": source_run["workflow_url"],
        "created_at": first_attempt["created_at"],
    }
    title = str(first_attempt.get("display_title", ""))
    match = DISCORD_RUN_TITLE_RE.fullmatch(title)
    if match is None:
        raise RuntimeError("Первый запуск цели не содержит SHA релиза в заголовке")
    first_sha = match.group("sha")

    runs: list[Any] = []
    page = 1
    while True:
        response = get_past_runs(sess, checkpoint, page)
        page_runs = response["workflow_runs"]
        runs.extend(page_runs)
        if len(page_runs) < 100:
            break
        page += 1

    runs.sort(key=lambda run: run["created_at"], reverse=True)
    fallback = next(
        (
            run
            for run in runs
            if published_run_sha(run)
        ),
        None,
    )
    first_release_found = False
    for run in runs:
        sha = published_run_sha(run)
        if not first_release_found:
            if sha == first_sha:
                first_release_found = True
            continue
        if sha and sha != first_sha:
            return run

    if not first_release_found and fallback is not None:
        return fallback
    raise RuntimeError(f"Не найден успешный релиз перед первым запуском цели {first_sha}")


def get_last_changelog(changelog_file: Path | None = None) -> str:
    changelog_file = validate_changelog_path(
        str(changelog_file) if changelog_file else CHANGELOG_FILE,
    )
    github_repository = require_environment("GITHUB_REPOSITORY")
    github_run = require_environment("GITHUB_RUN_ID")
    github_token = require_environment("GITHUB_TOKEN")
    target_id = validate_target_id(require_environment("CHANGELOG_TARGET_ID"))

    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {github_token}"
    session.headers["Accept"] = "application/vnd.github+json"
    session.headers["X-GitHub-Api-Version"] = "2022-11-28"

    source_run = require_environment("SOURCE_WORKFLOW_RUN_ID")
    most_recent = get_most_recent_workflow(
        session,
        github_repository,
        github_run,
        target_id,
    )
    target_checkpoint_found = most_recent is not None
    last_sha = None
    if most_recent is not None:
        title = str(most_recent.get("display_title", ""))
        match = DISCORD_RUN_TITLE_RE.fullmatch(title)
        if match is not None:
            last_sha = match.group("sha")

    if last_sha is None:
        first_attempt = get_earliest_target_attempt(
            session,
            github_repository,
            github_run,
            target_id,
        )
        most_recent = get_source_release_before_attempt(
            session,
            github_repository,
            source_run,
            first_attempt,
        )
        last_sha = published_run_sha(most_recent) if isinstance(most_recent, Mapping) else None

    if not last_sha:
        raise RuntimeError("Не найден предыдущий успешный запуск с SHA опубликованного релиза")

    print(f"Last successful publish job was {most_recent['id']}: {last_sha}")
    last_changelog_stream = get_last_changelog_by_sha(
        session,
        last_sha,
        github_repository,
        changelog_file,
        allow_missing=not target_checkpoint_found,
    )

    return last_changelog_stream


def get_last_changelog_by_sha(
    sess: requests.Session,
    sha: str,
    github_repository: str,
    changelog_file: Path | None = None,
    *,
    allow_missing: bool = False,
) -> str:
    """
    Получает предыдущую версию YAML-чейнджлога через GitHub API, поскольку Actions использует неглубокий клон.
    """
    changelog_file = validate_changelog_path(
        str(changelog_file) if changelog_file else CHANGELOG_FILE,
    )
    params = {
        "ref": sha,
    }
    headers = {"Accept": "application/vnd.github.raw"}
    encoded_changelog_path = quote(changelog_file.as_posix(), safe="/")

    resp = sess.get(
        f"{GITHUB_API_URL}/repos/{github_repository}/contents/{encoded_changelog_path}",
        headers=headers,
        params=params,
        timeout=HTTP_REQUEST_TIMEOUT,
    )
    if allow_missing and resp.status_code == 404:
        return "Entries: []\n"
    resp.raise_for_status()
    return resp.text


def get_released_changelog(changelog_file: Path, released_sha: str) -> str:
    github_repository = require_environment("GITHUB_REPOSITORY")
    github_token = require_environment("GITHUB_TOKEN")

    session = requests.Session()
    session.headers["Authorization"] = f"Bearer {github_token}"
    session.headers["Accept"] = "application/vnd.github+json"
    session.headers["X-GitHub-Api-Version"] = "2022-11-28"
    return get_last_changelog_by_sha(
        session,
        released_sha,
        github_repository,
        changelog_file,
    )


def diff_changelog(
    old: dict[str, Any], cur: dict[str, Any]
) -> Iterable[ChangelogEntry]:
    """
    Находит новые записи, которых не было в предыдущей публикации.
    """
    old_entries = old.get("Entries")
    current_entries = cur.get("Entries")
    if not isinstance(old_entries, list) or not isinstance(current_entries, list):
        raise RuntimeError("Обе версии чейнжлога должны содержать список Entries")
    if not all(isinstance(entry, Mapping) for entry in old_entries + current_entries):
        raise RuntimeError("Все элементы Entries должны быть YAML-отображениями")

    def unique_ids(entries: list[ChangelogEntry]) -> set[int] | None:
        ids = [entry.get("id") for entry in entries]
        if any(type(entry_id) is not int or entry_id <= 0 for entry_id in ids):
            return None
        unique = set(ids)
        return unique if len(unique) == len(ids) else None

    old_ids = unique_ids(old_entries)
    current_ids = unique_ids(current_entries)
    if old_ids is not None and current_ids is not None:
        return (entry for entry in current_entries if entry["id"] not in old_ids)

    old_identities = {changelog_entry_identity(entry) for entry in old_entries}
    return (
        entry
        for entry in current_entries
        if changelog_entry_identity(entry) not in old_identities
    )


def _send_discord_payload(
    payload: dict[str, Any],
    deadline: float | None = None,
    files: list[tuple[str, tuple[str, bytes, str]]] | None = None,
) -> None:
    if deadline is None:
        deadline = time.monotonic() + DISCORD_PUBLISH_TIMEOUT
    send_message(
        DISCORD_WEBHOOK_URL,
        payload,
        files=files,
        attempts=DISCORD_RETRY_LIMIT + 1,
        timeout=HTTP_REQUEST_TIMEOUT,
        deadline=deadline,
    )


def send_embed_discord(embed: dict, deadline: float | None = None) -> None:
    _send_discord_payload({"embeds": [embed]}, deadline)


def send_multipart_discord(
    payload: dict[str, Any],
    files: list[tuple[str, tuple[str, bytes, str]]],
    deadline: float | None = None,
) -> None:
    _send_discord_payload(payload, deadline, files)


def split_message(message: str, limit: int = DISCORD_SPLIT_LIMIT) -> list[str]:
    return textwrap.wrap(message, width=limit, replace_whitespace=False)


def _append_text_components(components: list[dict[str, Any]], content: str) -> None:
    components.extend(
        {"type": 10, "content": part}
        for part in split_message(content)
        if part
    )


def _media_components(media: list[DiscordMedia]) -> list[dict[str, Any]]:
    components: list[dict[str, Any]] = []
    downloaded: list[DownloadedMedia] = []
    remote: list[RemoteMedia] = []

    def flush_downloaded() -> None:
        if not downloaded:
            return
        components.append({
            "type": 12,
            "items": [
                {
                    "media": {"url": f"attachment://{item.filename}"},
                    **({"description": item.description[:1024]} if item.description else {}),
                }
                for item in downloaded
            ],
        })
        downloaded.clear()

    def flush_remote() -> None:
        if not remote:
            return
        lines = []
        for item in remote:
            description = " ".join((item.description or "Внешнее медиа").split())
            lines.append(f"🔗 **{description}**\n<{item.url}>")
        _append_text_components(components, "\n".join(lines))
        remote.clear()

    for item in media:
        if isinstance(item, DownloadedMedia):
            flush_remote()
            downloaded.append(item)
        else:
            flush_downloaded()
            remote.append(item)

    flush_downloaded()
    flush_remote()
    return components


def build_media_payload(
    text_embed: dict[str, Any] | None,
    media: list[DiscordMedia],
    entry: ChangelogEntry | None = None,
) -> tuple[dict[str, Any], list[tuple[str, tuple[str, bytes, str]]]]:
    components: list[dict[str, Any]] = []
    if text_embed is not None:
        components.append({"type": 10, "content": f"**{text_embed['title']}**"})
        components.append({"type": 14, "divider": True, "spacing": 1})

    if entry is None:
        if text_embed is not None:
            _append_text_components(components, text_embed["description"])
        components.extend(_media_components(media))
    else:
        pending_lines: list[str] = []
        separate_from_previous_media = False
        selected_indices = {item.change_index for item in media if item.change_index is not None}
        for emoji, title, items in grouped_changes(entry["changes"]):
            visible_items = [
                (index, change)
                for index, change in items
                if text_embed is not None or index in selected_indices
            ]
            if not visible_items:
                continue

            group_started = False
            for index, change in visible_items:
                if separate_from_previous_media:
                    components.append({"type": 14, "divider": True, "spacing": 2})
                    separate_from_previous_media = False
                if not group_started:
                    if pending_lines:
                        pending_lines.append("")
                    pending_lines.append(f"{emoji} **{title}**")
                    group_started = True

                pending_lines.append(f"• {change['message']}")
                change_media = [item for item in media if item.change_index == index]
                if change_media:
                    _append_text_components(components, "\n".join(pending_lines))
                    pending_lines.clear()
                    components.extend(_media_components(change_media))
                    separate_from_previous_media = True
                    group_started = False

        if pending_lines:
            _append_text_components(components, "\n".join(pending_lines))

        root_media = [item for item in media if item.change_index is None]
        if text_embed is not None and (url := entry.get("url")) and url.strip():
            if separate_from_previous_media:
                components.append({"type": 14, "divider": True, "spacing": 1})
            _append_text_components(components, f"[GitHub Pull Request]({url})")
            separate_from_previous_media = False
        if root_media:
            if components:
                components.append({"type": 14, "divider": True, "spacing": 2})
            components.extend(_media_components(root_media))

    downloaded = [item for item in media if isinstance(item, DownloadedMedia)]
    files = [
        (
            f"files[{index}]",
            (item.filename, item.data, item.content_type),
        )
        for index, item in enumerate(downloaded)
    ]
    attachments = [
        {
            "id": index,
            "filename": item.filename,
            **({"description": item.description[:1024]} if item.description else {}),
        }
        for index, item in enumerate(downloaded)
    ]
    return {
        "flags": DISCORD_COMPONENTS_V2_FLAG,
        "components": [{
            "type": 17,
            "accent_color": text_embed.get("color", 0x3498db) if text_embed else 0x3498db,
            "components": components,
        }],
        "attachments": attachments,
        "allowed_mentions": {"parse": []},
    }, files


def send_changelog_text(embed: dict[str, Any], deadline: float | None = None) -> None:
    payload, _ = build_media_payload(embed, [])
    _send_discord_payload(payload, deadline)


def iter_entry_media_batches(
    entry: ChangelogEntry,
    deadline: float | None = None,
) -> Iterable[list[DiscordMedia]]:
    records: list[tuple[Any, int | None]] = []
    for change_index, change in enumerate(entry.get("changes") or []):
        if not isinstance(change, Mapping):
            report_media_warning(str(change), "запись changes имеет неверный формат")
            continue
        change_records = change.get("media") or []
        if not isinstance(change_records, list):
            report_media_warning(str(change_records), "поле media имеет неверный формат")
            continue
        records.extend((record, change_index) for record in change_records)

    entry_records = entry.get("media") or []
    if not isinstance(entry_records, list):
        report_media_warning(str(entry_records), "поле media имеет неверный формат")
    else:
        for record in entry_records:
            change_index = record.get("change") if isinstance(record, dict) else None
            if type(change_index) is not int or not 0 <= change_index < len(entry.get("changes") or []):
                change_index = None
            records.append((record, change_index))

    if len(records) > MEDIA_MAX_FILES_PER_ENTRY:
        first_ignored = records[MEDIA_MAX_FILES_PER_ENTRY][0]
        ignored_url = (
            first_ignored.get("url", "<неизвестно>")
            if isinstance(first_ignored, dict)
            else first_ignored
        )
        report_media_warning(
            str(ignored_url),
            f"превышен предел {MEDIA_MAX_FILES_PER_ENTRY} файлов на запись",
        )
        records = records[:MEDIA_MAX_FILES_PER_ENTRY]

    batch: list[DiscordMedia] = []
    batch_size = 0
    for index, (record, change_index) in enumerate(records, start=1):
        if not isinstance(record, dict) or not isinstance(record.get("url"), str):
            report_media_warning(str(record), "запись media не содержит URL")
            continue

        url = record["url"]
        description = record.get("description")
        if not isinstance(description, str):
            description = None
        try:
            item = download_media(url, description, f"media-{index}", deadline)
        except DiscordPublishTimeoutError:
            raise
        except UnsafeMediaUrlError as error:
            report_media_warning(url, str(error))
            continue
        except Exception as error:
            print(f"::notice::Медиа {url} передаётся Discord как внешняя ссылка: {error}")
            item = RemoteMedia(url, description, change_index)
        else:
            item = DownloadedMedia(
                item.url,
                item.description,
                item.data,
                item.content_type,
                item.filename,
                change_index,
            )

        item_size = len(item.data) if isinstance(item, DownloadedMedia) else 0
        if batch and (
            len(batch) >= MEDIA_MAX_FILES_PER_REQUEST
            or batch_size + item_size > MEDIA_MAX_REQUEST_SIZE
        ):
            yield batch
            batch = []
            batch_size = 0
        batch.append(item)
        batch_size += item_size

    if batch:
        yield batch

def send_media_batch(
    batch: list[DiscordMedia],
    text_embed: dict[str, Any] | None,
    deadline: float,
    entry: ChangelogEntry | None = None,
) -> None:
    try:
        payload, files = build_media_payload(text_embed, batch, entry)
        if files:
            send_multipart_discord(payload, files, deadline)
        else:
            _send_discord_payload(payload, deadline)
    except DiscordPublishTimeoutError:
        raise
    except Exception as error:
        for item in batch:
            report_media_warning(item.url, f"не удалось отправить файл: {error}")
        if text_embed is not None:
            send_changelog_text(text_embed, deadline)


def send_to_discord(entries: Iterable[ChangelogEntry]) -> None:
    if not DISCORD_WEBHOOK_URL:
        raise RuntimeError("Переменная DISCORD_WEBHOOK_URL не задана")

    deadline = time.monotonic() + DISCORD_PUBLISH_TIMEOUT
    for entry in entries:
        parts = format_grouped_change_parts(entry["changes"], entry.get("url"))

        embeds = [
            {
                "title": f"👤 {entry['author']}",
                "description": part,
                "color": 0x3498db
            }
            for part in parts
            if part
        ]
        if not embeds:
            continue

        last_embed = embeds[-1]
        media_batches = iter_entry_media_batches(entry, deadline)
        try:
            first_batch = next(media_batches)
        except StopIteration:
            for embed in embeds:
                send_changelog_text(embed, deadline)
            continue

        for embed in embeds[:-1]:
            send_changelog_text(embed, deadline)

        media_entry = entry if len(embeds) == 1 else None
        send_media_batch(first_batch, last_embed, deadline, media_entry)

        for batch in media_batches:
            send_media_batch(batch, None, deadline, media_entry)


if __name__ == "__main__":
    main()
