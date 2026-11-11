#!/usr/bin/env python3

import argparse
import base64
import json
import os
import re
import subprocess
import sys
import unicodedata
from dataclasses import dataclass, field
from datetime import datetime, timezone
from html import escape
from html.parser import HTMLParser
from pathlib import Path
from typing import Any
from urllib.error import HTTPError
from urllib.parse import quote, urlencode
from urllib.request import Request, urlopen

import yaml

from changelog_path import validate_changelog_path
from changelog_schema import ChangelogIssue, format_issue_for_user, inspect_changelog_document


MAIN_CATEGORY = "Main"


def configured_changelog_file() -> Path:
    return validate_changelog_path(os.environ.get("CHANGELOG_FILE"))


CHANGELOG_FILE = configured_changelog_file()
CATEGORY_FILES = {
    MAIN_CATEGORY: CHANGELOG_FILE.name,
}
WORKFLOW_FILE = "sunrise-changelog.yml"
PARTS_PATH = Path("Resources/Changelog/Parts")
CHANGELOG_PATH = CHANGELOG_FILE.parent
PULL_REQUEST_TEMPLATE_PATH = Path(".github/PULL_REQUEST_TEMPLATE.md")
UPDATER_PATH = Path("Tools/_sunrise/changelog/update_changelog.py")

COMMENT_RE = re.compile(r"(?<!\\)<!--([\s\S]*?)(?<!\\)-->")
COMMENT_PLACEHOLDER = "\0"
FENCE_RE = re.compile(r"^[ ]{0,3}(`{3,}|~{3,})")
MARKER_RE = re.compile(r"^\s*(?::cl:|🆑)", re.IGNORECASE | re.MULTILINE)
HEADER_RE = re.compile(
    r"^[ \t]*(?::cl:|🆑)[ \t]*([^\r\n]*?)[ \t]*\r?$",
    re.IGNORECASE | re.MULTILINE,
)
CI_MARKER_RE = re.compile(r"^\s*:ci:", re.IGNORECASE | re.MULTILINE)
CI_HEADER_RE = re.compile(
    r"^[ \t]*:ci:[ \t]*([^\r\n]*?)[ \t]*\r?$",
    re.IGNORECASE | re.MULTILINE,
)
ENTRY_RE = re.compile(
    r"^ *[*-]? *(add|remove|tweak|fix|bug|bugfix): *([^\n\r]+)\r?$",
    re.IGNORECASE,
)
MALFORMED_ENTRY_RE = re.compile(r"^ *[*-] *(?:[a-z]+):", re.IGNORECASE)
CATEGORY_RE = re.compile(r"^\s*([a-z]+):\s*$", re.IGNORECASE)
MEDIA_RE = re.compile(r"^\s*media:\s*(.+?)\s*$", re.IGNORECASE)
MEDIA_MARKDOWN_RE = re.compile(r"^!?\[([^\]]*)\]\(\s*([^()\s]+)\s*\)$")
MEDIA_URL_RE = re.compile(r"^[A-Za-z][A-Za-z0-9+.-]*://\S+$")
MARKDOWN_HEADER_RE = re.compile(r"^[ \t]{0,3}(#{1,6})[ \t]+(.+?)[ \t]*#*[ \t]*$")
MEDIA_SECTION_TITLE_RE = re.compile(
    r"^(?:медиа|media)(?=$|[\s(:/|—-])",
    re.IGNORECASE,
)
MEDIA_SECTION_MARKER_RE = re.compile(
    r"^\s*<!--\s*changelog-media-section\s*-->\s*$",
    re.IGNORECASE,
)
END_MARKER_RE = re.compile(r"^\s*:end-cl:\s*$", re.IGNORECASE)
CHANGE_TYPES = {
    "add": "Add",
    "remove": "Remove",
    "tweak": "Tweak",
    "fix": "Fix",
    "bug": "Fix",
    "bugfix": "Fix",
}
STATUS_FORMAT = {
    "success": ("notice", "✅"),
    "skip": ("notice", "⏭️"),
    "warning": ("warning", "⚠️"),
    "error": ("error", "❌"),
}


def _mask_ignored_text(value: str) -> str:
    def mask(text: str) -> str:
        return "".join(char if char in "\r\n" else COMMENT_PLACEHOLDER for char in text)

    text = COMMENT_RE.sub(lambda match: mask(match.group()), value)
    result: list[str] = []
    fence_character: str | None = None
    fence_length = 0
    for line in text.splitlines(keepends=True):
        content = line.rstrip("\r\n")
        if fence_character is None:
            if match := FENCE_RE.match(content):
                fence = match.group(1)
                fence_character = fence[0]
                fence_length = len(fence)
                result.append(mask(line))
            else:
                result.append(line)
            continue

        result.append(mask(line))
        closing = content.lstrip(" ")
        if (
            len(content) - len(closing) <= 3
            and len(closing.rstrip(" \t")) >= fence_length
            and set(closing.rstrip(" \t")) == {fence_character}
        ):
            fence_character = None

    return "".join(result)


@dataclass(frozen=True)
class ParsedCategory:
    name: str
    changes: list[dict[str, str]]
    media: list[dict[str, Any]] = field(default_factory=list)


class _MediaHTMLParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.attributes: dict[str, str | None] | None = None

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag == "img" and self.attributes is None:
            self.attributes = dict(attrs)

    def handle_startendtag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        self.handle_starttag(tag, attrs)


def report_status(status: str, message: str) -> None:
    command, icon = STATUS_FORMAT[status]
    escaped = message.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
    print(f"::{command}::{escaped}")

    if summary_path := os.environ.get("GITHUB_STEP_SUMMARY"):
        summary_file = Path(summary_path)
        is_empty = not summary_file.exists() or summary_file.stat().st_size == 0
        with summary_file.open("a", encoding="utf-8") as summary:
            if is_empty:
                summary.write("## Автоматический чейнджлог\n\n")
            summary.write(f"- {icon} {message}\n")


def write_validation_summary(title: str, content: str) -> None:
    if summary_path := os.environ.get("GITHUB_STEP_SUMMARY"):
        with Path(summary_path).open("a", encoding="utf-8") as summary:
            summary.write(f"## {title}\n\n{content}\n")


def list_changed_changelog_paths(number: int) -> list[str]:
    repository = os.environ.get("GITHUB_REPOSITORY", "")
    if not repository or "/" not in repository:
        return []

    filenames = {CHANGELOG_FILE.name.casefold()}
    for category in os.environ.get("CHANGELOG_EXTRA_CATEGORIES", "").split(","):
        category = category.strip()
        if re.fullmatch(r"[A-Za-z]+", category):
            filenames.add(f"{category}.yml".casefold())

    paths: list[str] = []
    page = 1
    while True:
        files = github_request(
            f"/repos/{repository}/pulls/{number}/files",
            {"per_page": 100, "page": page},
        )
        if not isinstance(files, list):
            raise RuntimeError("GitHub API не вернул список файлов PR")

        for item in files:
            if not isinstance(item, dict) or not isinstance(item.get("filename"), str):
                continue
            filename = item["filename"]
            path = Path(filename)
            if (
                path.parent == CHANGELOG_PATH
                and path.suffix.casefold() in {".yml", ".yaml"}
                and path.name.casefold() in filenames
            ):
                paths.append(filename)

        if len(files) < 100:
            return sorted(set(paths))
        page += 1


def load_repository_file(path: str, ref: str) -> str:
    repository = os.environ.get("GITHUB_REPOSITORY", "")
    response = github_request(
        f"/repos/{repository}/contents/{quote(path, safe='/')}",
        {"ref": ref},
    )
    if not isinstance(response, dict):
        raise RuntimeError(f"GitHub API не вернул содержимое файла {path}")
    if response.get("encoding") == "none" and isinstance(response.get("sha"), str):
        response = github_request(f"/repos/{repository}/git/blobs/{response['sha']}")
    if not isinstance(response, dict) or not isinstance(response.get("content"), str):
        raise RuntimeError(f"GitHub API не вернул содержимое файла {path}")
    try:
        return base64.b64decode(response["content"]).decode("utf-8-sig")
    except (ValueError, UnicodeDecodeError) as error:
        raise RuntimeError(f"Не удалось декодировать файл {path}") from error


def validate_changed_changelog_files(pull_request: dict[str, Any]) -> None:
    if not os.environ.get("GITHUB_TOKEN") or not os.environ.get("GITHUB_REPOSITORY"):
        return

    number = int(pull_request["number"])
    head = pull_request.get("head")
    head_sha = head.get("sha") if isinstance(head, dict) else None
    if not isinstance(head_sha, str) or not head_sha:
        raise RuntimeError(f"PR #{number}: не найден SHA ветки для проверки чейнжлога")

    reports: list[tuple[str, list[ChangelogIssue]]] = []
    for path in list_changed_changelog_paths(number):
        try:
            document = yaml.safe_load(load_repository_file(path, head_sha))
            issues = inspect_changelog_document(document)
        except yaml.YAMLError as error:
            issues = [
                ChangelogIssue(
                    "yaml",
                    f"Файл {path}: YAML не удалось разобрать: {getattr(error, 'problem', None) or error}.",
                    "Исправьте синтаксис YAML вручную.",
                ),
            ]
        reports.append((path, issues))

    failed = False
    for path, issues in reports:
        if not issues:
            report_status("success", f"{path}: проверка структуры завершена, ошибок нет.")
            write_validation_summary(
                f"Структура {path}",
                """### Результат

✅ Проверка пройдена, ошибок структуры не найдено.

### Что это значит

Файл можно объединять с основной веткой. Авторемонт после слияния не потребуется.
""",
            )
            continue

        failed = True
        found = []
        solutions = []
        for index, issue in enumerate(issues, 1):
            report_status("error", f"{path}: {format_issue_for_user(issue)}")
            found.append(f"{index}. {escape(issue.message)}")
            solutions.append(f"{index}. {escape(issue.suggestion)}")
        write_validation_summary(
            f"Проверка структуры {path}",
            "\n".join(
                (
                    "### Результат",
                    "",
                    f"❌ Проверка не пройдена. Найдено ошибок: {len(issues)}.",
                    "",
                    "### Что обнаружено",
                    "",
                    *found,
                    "",
                    "### Как исправить",
                    "",
                    *solutions,
                ),
            ),
        )

    if failed:
        raise RuntimeError(f"PR #{number}: изменённый файл чейнжлога не прошёл проверку структуры")


def validate_pull_request_event(repo_root: Path, event_path: Path) -> None:
    event = json.loads(event_path.read_text(encoding="utf-8"))
    pull_request = event.get("pull_request")
    if not isinstance(pull_request, dict):
        raise RuntimeError("Событие не содержит pull_request")

    number = int(pull_request["number"])
    validate_changed_changelog_files(pull_request)
    body = pull_request.get("body")
    template = load_pull_request_template(repo_root)
    if is_changelog_template(body, template):
        report_status("success", f"PR #{number}: проверка завершена, шаблон чейнжлога не заполнен.")
        write_validation_summary(
            "Проверка чейнджлога",
            """### Результат

✅ Проверка пройдена. В описании PR оставлен пустой шаблон чейнжлога.

### Что это значит

После слияния этого PR запись в чейнжлог добавлена не будет.

### Что делать дальше

Если изменение должно быть видно игрокам, заполните блок `:cl:` в описании PR.
""",
        )
        return

    category_names = [MAIN_CATEGORY]
    for category in os.environ.get("CHANGELOG_EXTRA_CATEGORIES", "").split(","):
        category = category.strip()
        if not category:
            continue
        if re.fullmatch(r"[A-Za-z]+", category) is None:
            raise RuntimeError(f"Недопустимое имя категории чейнджлога: {category}")
        category_names.append(category)

    try:
        parsed = parse_pr_body(
            body,
            pull_request.get("user", {}).get("login", "unknown"),
            tuple(category_names),
        )
        if parsed is not None and not parsed[1]:
            raise ValueError("маркер чейнджлога найден, но ни одну запись изменений распознать не удалось")
    except ValueError as error:
        write_validation_summary(
            "Проверка чейнжлога",
            "\n".join(
                (
                    "### Результат",
                    "",
                    "❌ Проверка не пройдена: описание PR содержит некорректный чейнжлог.",
                    "",
                    "### Что обнаружено",
                    "",
                    f"<pre><code>{escape(str(error))}</code></pre>",
                    "",
                    "### Как исправить",
                    "",
                    "Исправьте блок `:cl:` по примеру из шаблона PR и запустите проверку повторно.",
                ),
            ),
        )
        raise RuntimeError(f"PR #{number}: чейнджлог не прошёл проверку") from error

    if parsed is None:
        report_status("success", f"PR #{number}: проверка завершена, блок чейнжлога отсутствует.")
        write_validation_summary(
            "Проверка чейнджлога",
            """### Результат

✅ Проверка пройдена. Блок чейнжлога в описании PR отсутствует.

### Что это значит

После слияния этого PR запись в чейнжлог добавлена не будет.

### Что делать дальше

Если изменение должно быть видно игрокам, добавьте блок `:cl:` по примеру из шаблона PR.
""",
        )
        return

    author, categories = parsed
    normalized = {
        "author": author,
        "categories": [
            {
                "name": category.name,
                "changes": category.changes,
                "media": category.media,
            }
            for category in categories
        ],
    }
    preview = yaml.safe_dump(normalized, allow_unicode=True, sort_keys=False)
    report_status("success", f"PR #{number}: проверка завершена, чейнжлог распознан.")
    write_validation_summary(
        "Проверка чейнжлога",
        "\n".join(
            (
                "### Результат",
                "",
                "✅ Проверка пройдена. Чейнжлог распознан без ошибок.",
                "",
                "### Что это значит",
                "",
                "После слияния PR бот добавит показанные ниже данные в чейнжлог.",
                "",
                "### Предварительный результат",
                "",
                f"<pre><code>{escape(preview)}</code></pre>",
            ),
        ),
    )


def parse_time(value: str) -> datetime:
    date, separator, remainder = value.partition("T")
    if separator:
        year, month, day = date.split("-")
        value = f"{int(year):04d}-{int(month):02d}-{int(day):02d}T{remainder}"
    return datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone(timezone.utc)


def format_changelog_time(value: str | None) -> str:
    timestamp = parse_time(value) if value else datetime.now(timezone.utc)
    return timestamp.strftime("%Y-%m-%dT%H:%M:%S.") + f"{timestamp.microsecond:06d}0+00:00"


def parse_media_value(value: str) -> dict[str, Any]:
    source = value.strip()
    markdown = MEDIA_MARKDOWN_RE.fullmatch(source)
    if markdown:
        url = markdown.group(2)
        description = markdown.group(1).strip()
        result = {"url": url}
        if description:
            result["description"] = description
        return result

    if source.casefold().startswith("<img") and source.endswith(">"):
        parser = _MediaHTMLParser()
        parser.feed(source)
        if parser.attributes is not None:
            url = (parser.attributes.get("src") or "").strip()
            if MEDIA_URL_RE.fullmatch(url):
                result = {"url": url}
                description = (parser.attributes.get("alt") or "").strip()
                if description:
                    result["description"] = description
                return result

    url = source
    if MEDIA_URL_RE.fullmatch(url):
        return {"url": url}

    raise ValueError(f"не удалось распознать строку медиа: media: {value.strip()[:120]}")


def parse_pr_media_section(body: str | None) -> list[dict[str, Any]]:
    source = body or ""
    marker_lines = {
        index
        for index, line in enumerate(source.splitlines())
        if MEDIA_SECTION_MARKER_RE.match(line)
    }
    text = _mask_ignored_text(source)
    media: list[dict[str, Any]] = []
    pending_description: list[str] = []
    in_media_section = False

    for line_index, source_line in enumerate(text.splitlines()):
        had_ignored_text = COMMENT_PLACEHOLDER in source_line
        line = source_line.replace(COMMENT_PLACEHOLDER, "")
        heading = MARKDOWN_HEADER_RE.match(line)

        if line_index in marker_lines:
            in_media_section = True
            pending_description.clear()
            continue

        if not in_media_section:
            if MARKER_RE.match(line):
                return []
            if (
                heading is not None
                and len(heading.group(1)) == 2
                and MEDIA_SECTION_TITLE_RE.match(heading.group(2).strip())
            ):
                in_media_section = True
            continue

        if heading is not None or MARKER_RE.match(line):
            break
        if had_ignored_text or not line.strip():
            continue

        source = line.strip()
        if media_match := MEDIA_RE.match(source):
            source = media_match.group(1)
        try:
            item = parse_media_value(source)
        except ValueError:
            pending_description.append(line.strip())
            continue

        if pending_description:
            item["description"] = " ".join(pending_description)
            pending_description.clear()
        media.append(item)

    return media


def parse_pr_body(
    body: str | None,
    fallback_author: str,
    category_names: tuple[str, ...] = tuple(CATEGORY_FILES),
    allow_inline_media: bool = False,
) -> tuple[str, list[ParsedCategory]] | None:
    section_media = parse_pr_media_section(body)
    text = _mask_ignored_text(body or "")
    header = HEADER_RE.search(text)
    if header is None:
        if MARKER_RE.search(text):
            raise ValueError("маркер чейнжлога найден, но его заголовок не удалось распознать")
        return None

    author = header.group(1).strip() if header.group(1) else fallback_author
    current_category = MAIN_CATEGORY
    entries: dict[str, dict[str, list[dict[str, Any]]]] = {}
    current_change: dict[str, str] | None = None
    empty_lines = 0

    for line in text[header.end():].splitlines():
        had_comment = COMMENT_PLACEHOLDER in line
        line = line.replace(COMMENT_PLACEHOLDER, "")
        if END_MARKER_RE.match(line):
            break
        if not line.strip():
            current_change = None
            if had_comment:
                continue
            empty_lines += 1
            if empty_lines >= 2:
                break
            continue
        empty_lines = 0

        category_match = CATEGORY_RE.match(line)
        if category_match:
            requested = category_match.group(1)
            matched = next((name for name in category_names if name.casefold() == requested.casefold()), None)
            if matched is not None:
                current_category = matched
                current_change = None
            continue

        media_match = MEDIA_RE.match(line)
        if media_match:
            if allow_inline_media:
                category = entries.setdefault(current_category, {"changes": [], "media": []})
                if not category["changes"]:
                    raise ValueError(f"категория {current_category} содержит медиа, но не содержит записей изменений")
                media = parse_media_value(media_match.group(1))
                media["change"] = len(category["changes"]) - 1
                category["media"].append(media)
            else:
                report_status(
                    "warning",
                    "Строка media: вне раздела «## Медиа» или «## Media» проигнорирована.",
                )
            continue

        try:
            parse_media_value(line)
        except ValueError:
            pass
        else:
            continue

        entry_match = ENTRY_RE.match(line)
        if entry_match is None:
            if MALFORMED_ENTRY_RE.match(line):
                raise ValueError(f"не удалось распознать строку чейнжлога: {line.strip()[:120]}")
            if current_change is not None:
                current_change["message"] += f"\n{line.strip()}"
            continue

        change_type = CHANGE_TYPES[entry_match.group(1).lower()]
        category = entries.setdefault(current_category, {"changes": [], "media": []})
        current_change = {"type": change_type, "message": entry_match.group(2).strip()}
        category["changes"].append(current_change)

    if section_media and entries:
        media_category = MAIN_CATEGORY if MAIN_CATEGORY in entries else next(iter(entries))
        entries[media_category]["media"].extend(section_media)

    return author, [
        ParsedCategory(name, category["changes"], category["media"])
        for name, category in entries.items()
    ]


def parse_manual_changelog(
    body: str | None,
    category_names: tuple[str, ...] = tuple(CATEGORY_FILES),
) -> tuple[str, list[ParsedCategory]]:
    source = body or ""
    text = _mask_ignored_text(source)
    header = CI_HEADER_RE.search(text)
    if header is None:
        if CI_MARKER_RE.search(text):
            raise ValueError("маркер :ci: найден, но его заголовок не удалось распознать")
        raise ValueError("ручной чейнжлог должен начинаться со строки :ci: Автор")

    author = header.group(1).replace(COMMENT_PLACEHOLDER, "").strip()
    if not author:
        raise ValueError("после :ci: необходимо указать имя автора")

    normalized = f":cl: {author}{source[header.end():]}"
    parsed = parse_pr_body(normalized, "", category_names, allow_inline_media=True)
    if parsed is None:
        raise ValueError("не удалось разобрать ручной чейнжлог")
    return parsed


def changelog_block(body: str | None) -> str | None:
    text = _mask_ignored_text(body or "")
    marker = MARKER_RE.search(text)
    if marker is None:
        return None

    block: list[str] = []
    empty_lines = 0
    for line in text[marker.start():].splitlines(keepends=True):
        had_ignored_text = COMMENT_PLACEHOLDER in line
        line = line.replace(COMMENT_PLACEHOLDER, "")
        if END_MARKER_RE.match(line):
            block.append(line)
            break
        if not line.strip():
            if had_ignored_text:
                continue
            empty_lines += 1
            if empty_lines >= 2:
                break
        else:
            empty_lines = 0
        block.append(line)

    return "".join(block)


def normalize_template_changelog(value: str) -> str:
    return "".join(
        character
        for character in value
        if not character.isspace() and unicodedata.category(character) not in {"Cc", "Cf"}
    )


def is_changelog_template(body: str | None, template: str | None) -> bool:
    body_block = changelog_block(body)
    template_block = changelog_block(template)
    return bool(
        body_block
        and template_block
        and normalize_template_changelog(body_block) == normalize_template_changelog(template_block)
    )


def load_pull_request_template(repo_root: Path) -> str | None:
    path = repo_root / PULL_REQUEST_TEMPLATE_PATH
    return path.read_text(encoding="utf-8-sig") if path.is_file() else None


def github_request(
    path: str,
    query: dict[str, str | int] | None = None,
    token_environment: str = "GITHUB_TOKEN",
) -> Any:
    api_url = os.environ.get("GITHUB_API_URL", "https://api.github.com").rstrip("/")
    url = f"{api_url}{path}"
    if query:
        url += "?" + urlencode(query)

    headers = {
        "Accept": "application/vnd.github+json",
        "User-Agent": "Sunrise-Changelog-Actions",
        "X-GitHub-Api-Version": "2022-11-28",
    }
    if token := os.environ.get(token_environment):
        headers["Authorization"] = f"Bearer {token}"

    try:
        with urlopen(Request(url, headers=headers), timeout=30) as response:
            return json.load(response)
    except HTTPError as error:
        details = error.read().decode("utf-8", errors="replace")[:500]
        raise RuntimeError(f"GitHub API вернул {error.code} для {path}: {details}") from error


def repository_slug() -> str:
    slug = os.environ.get("GITHUB_REPOSITORY", "")
    if slug.count("/") != 1:
        raise RuntimeError("GITHUB_REPOSITORY должен иметь вид owner/repository")
    return slug


def load_event_pull_request(event_path: Path) -> dict[str, Any] | None:
    event = json.loads(event_path.read_text(encoding="utf-8"))
    pull_request = event.get("pull_request")
    return pull_request if isinstance(pull_request, dict) else None


def load_pull_request(number: int) -> dict[str, Any]:
    return github_request(f"/repos/{repository_slug()}/pulls/{number}")


def latest_changelog_time(repo_root: Path, category_files: dict[str, str] = CATEGORY_FILES) -> datetime:
    latest = datetime(1970, 1, 1, tzinfo=timezone.utc)
    for filename in category_files.values():
        path = repo_root / CHANGELOG_PATH / filename
        if not path.exists():
            continue

        document = yaml.safe_load(path.read_text(encoding="utf-8-sig")) or {}
        for entry in document.get("Entries", []):
            if value := entry.get("time"):
                latest = max(latest, parse_time(str(value)))
    return latest


def load_checkpoint(repo_root: Path, category_files: dict[str, str] = CATEGORY_FILES) -> datetime:
    response = github_request(
        f"/repos/{repository_slug()}/actions/workflows/{WORKFLOW_FILE}/runs",
        {"status": "success", "per_page": 100},
        token_environment="ACTIONS_TOKEN",
    )
    current_run_id = os.environ.get("GITHUB_RUN_ID")
    previous_runs = sorted(
        [
            run
            for run in response.get("workflow_runs", [])
            if str(run.get("id")) != current_run_id
            and run.get("event") == "pull_request_target"
        ],
        key=lambda run: parse_time(run["created_at"]),
        reverse=True,
    )
    for previous_run in previous_runs:
        jobs = github_request(
            f"/repos/{repository_slug()}/actions/runs/{previous_run['id']}/jobs",
            {"per_page": 100},
            token_environment="ACTIONS_TOKEN",
        )
        if any(
            job.get("name") == "update" and job.get("conclusion") == "success"
            for job in jobs.get("jobs", [])
        ):
            return parse_time(previous_run.get("run_started_at") or previous_run["created_at"])

    report_status(
        "skip",
        "Предыдущий успешный запуск Actions не найден: сверяем от последней записи чейнжлога.",
    )
    return latest_changelog_time(repo_root, category_files)


def list_merged_pull_requests(checkpoint: datetime) -> list[dict[str, Any]]:
    pulls: list[dict[str, Any]] = []
    page = 1

    while True:
        page_items = github_request(
            f"/repos/{repository_slug()}/pulls",
            {
                "state": "closed",
                "sort": "updated",
                "direction": "desc",
                "per_page": 100,
                "page": page,
            },
        )
        if not page_items:
            break

        reached_checkpoint = False
        for pull_request in page_items:
            updated_at = parse_time(pull_request["updated_at"])
            if updated_at < checkpoint:
                reached_checkpoint = True

            merged_at = pull_request.get("merged_at")
            if merged_at and parse_time(merged_at) >= checkpoint:
                pulls.append(pull_request)

        if reached_checkpoint or len(page_items) < 100:
            break
        page += 1

    return pulls


def load_known_urls(
    repo_root: Path,
    category_files: dict[str, str] = CATEGORY_FILES,
) -> dict[str, set[str]]:
    known = {category: set() for category in category_files}

    for category, filename in category_files.items():
        path = repo_root / CHANGELOG_PATH / filename
        if not path.exists():
            continue
        document = yaml.safe_load(path.read_text(encoding="utf-8-sig")) or {}
        known[category].update(
            str(entry["url"])
            for entry in document.get("Entries", [])
            if entry.get("url")
        )

    parts_dir = repo_root / PARTS_PATH
    for path in parts_dir.glob("*.yml"):
        part = yaml.safe_load(path.read_text(encoding="utf-8-sig")) or {}
        category = part.get("category", MAIN_CATEGORY)
        if category in known and part.get("url"):
            known[category].add(str(part["url"]))

    return known


def is_target_pull_request(pull_request: dict[str, Any], target_branch: str) -> bool:
    return bool(
        pull_request.get("merged_at")
        and pull_request.get("base", {}).get("ref") == target_branch
        and pull_request.get("merged", True)
    )


def write_pull_request_parts(
    repo_root: Path,
    pull_requests: list[dict[str, Any]],
    target_branch: str,
    category_files: dict[str, str] = CATEGORY_FILES,
    current_pull_request_number: int | None = None,
) -> int:
    known_urls = load_known_urls(repo_root, category_files)
    pull_request_template = load_pull_request_template(repo_root)
    written = 0

    for pull_request in sorted(pull_requests, key=lambda item: item.get("merged_at") or ""):
        number = int(pull_request["number"])
        if not is_target_pull_request(pull_request, target_branch):
            report_status("skip", f"PR #{number} пропущен: он не был слит в ветку {target_branch}.")
            continue

        if is_changelog_template(pull_request.get("body"), pull_request_template):
            report_status("skip", f"PR #{number} пропущен: оставлен шаблон чейнжлога.")
            continue

        try:
            parsed = parse_pr_body(
                pull_request.get("body"),
                pull_request.get("user", {}).get("login", "unknown"),
                tuple(category_files),
            )
            if parsed is not None and not parsed[1]:
                raise ValueError("маркер чейнжлога найден, но ни одну запись изменений распознать не удалось")
        except ValueError as error:
            if current_pull_request_number is None or number == current_pull_request_number:
                raise RuntimeError(f"PR #{number}: {error}") from error
            report_status("warning", f"PR #{number} пропущен из-за ошибки разбора: {error}.")
            continue
        if parsed is None:
            report_status("skip", f"PR #{number} пропущен: отсутствует маркер :cl: или 🆑.")
            continue

        author, categories = parsed
        url = str(pull_request["html_url"])
        merged_at = format_changelog_time(pull_request.get("merged_at"))
        pull_request_written = 0

        for category in categories:
            if url in known_urls[category.name]:
                continue

            part = {
                "author": author,
                "time": merged_at,
                "url": url,
                "changes": category.changes,
            }
            if category.media:
                part["media"] = category.media
            if category.name != MAIN_CATEGORY:
                part["category"] = category.name

            path = repo_root / PARTS_PATH / f"pr-{number}-{category.name}.yml"
            path.write_text(
                yaml.safe_dump(part, sort_keys=False, allow_unicode=True),
                encoding="utf-8",
            )
            known_urls[category.name].add(url)
            written += 1
            pull_request_written += 1

        if pull_request_written:
            report_status("success", f"PR #{number} обработан: подготовлено фрагментов — {pull_request_written}.")
        else:
            report_status("skip", f"PR #{number} пропущен: его записи уже присутствуют в чейнжлоге.")

    return written


def write_manual_parts(
    repo_root: Path,
    body: str,
    category_files: dict[str, str] = CATEGORY_FILES,
) -> int:
    author, categories = parse_manual_changelog(body, tuple(category_files))
    if not categories:
        raise RuntimeError("маркер :ci: найден, но ни одну запись изменений распознать не удалось")

    run_id = os.environ.get("GITHUB_RUN_ID", "local")
    if re.fullmatch(r"[A-Za-z0-9-]+", run_id) is None:
        raise RuntimeError("GITHUB_RUN_ID содержит недопустимые символы")

    repository = os.environ.get("GITHUB_REPOSITORY")
    url = f"https://github.com/{repository}/actions/runs/{run_id}" if repository else None
    timestamp = format_changelog_time(None)
    known_urls = load_known_urls(repo_root, category_files)
    written = 0

    for category in categories:
        if url and url in known_urls[category.name]:
            continue

        part = {
            "author": author,
            "time": timestamp,
            "url": url,
            "changes": category.changes,
        }
        if category.media:
            part["media"] = category.media
        if category.name != MAIN_CATEGORY:
            part["category"] = category.name

        path = repo_root / PARTS_PATH / f"manual-{run_id}-{category.name}.yml"
        path.write_text(
            yaml.safe_dump(part, sort_keys=False, allow_unicode=True),
            encoding="utf-8",
        )
        if url:
            known_urls[category.name].add(url)
        written += 1

    return written


def update_changelogs(
    repo_root: Path,
    category_files: dict[str, str] = CATEGORY_FILES,
) -> list[dict[str, Any]]:
    updater = repo_root / UPDATER_PATH
    parts = repo_root / PARTS_PATH
    reports: list[dict[str, Any]] = []

    for category, filename in category_files.items():
        changelog_file = repo_root / CHANGELOG_PATH / filename
        command = [
            sys.executable,
            str(updater),
            str(changelog_file),
            str(parts),
        ]
        if category != MAIN_CATEGORY:
            command.extend(["--category", category])
        try:
            result = subprocess.run(
                command,
                check=True,
                capture_output=True,
                text=True,
                encoding="utf-8",
            )
        except subprocess.CalledProcessError as error:
            if error.stdout:
                print(error.stdout, end="")
            if error.stderr:
                print(error.stderr, end="", file=sys.stderr)
            raise

        if result.stderr:
            print(result.stderr, end="", file=sys.stderr)
        try:
            report = json.loads(result.stdout)
        except json.JSONDecodeError as error:
            raise RuntimeError(f"Обновлятор не вернул JSON-отчёт для {changelog_file}") from error
        if not isinstance(report, dict) or not isinstance(report.get("repairs"), list):
            raise RuntimeError(f"Обновлятор вернул некорректный отчёт для {changelog_file}")
        report["file"] = changelog_file.relative_to(repo_root).as_posix()
        reports.append(report)

    return reports


def build_commit_message(reports: list[dict[str, Any]]) -> str:
    repair_count = sum(len(report["repairs"]) for report in reports)
    if not repair_count:
        return "Automatic changelog update [skip ci]\n"

    added = sum(int(report.get("added", 0)) for report in reports)
    lines = [
        "Automatic changelog update + fix [skip ci]",
        "",
        "## Автоматическое исправление чейнжлога",
        "",
        "### Результат",
        "",
        "✅ Структура чейнжлога исправлена автоматически.",
        f"📦 Добавлено новых записей: {added}.",
    ]
    for report in reports:
        repairs = report["repairs"]
        if not repairs:
            continue
        lines.extend(("", f"### Что исправлено в `{report['file']}`", ""))
        for index, repair in enumerate(repairs, 1):
            lines.append(f"{index}. Проблема: {repair['message']}")
            lines.append(f"   Решение: {repair['resolution']}")

    lines.extend(
        (
            "",
            "### Почему это было необходимо",
            "",
            "Отсутствующие, вложенные или повторяющиеся идентификаторы id ломают сравнение релизов",
            "и могут остановить либо пропустить публикацию в Discord.",
            "",
            "### Что не изменялось",
            "",
            "Текст, авторы, время, ссылки и медиа записей не изменялись.",
            "",
            "🤖 Исправление выполнено Sunrise-Bot.",
        ),
    )
    return "\n".join(lines) + "\n"


def write_commit_message(reports: list[dict[str, Any]]) -> None:
    output_path = os.environ.get("CHANGELOG_COMMIT_MESSAGE_FILE")
    if not output_path:
        return
    Path(output_path).write_text(build_commit_message(reports), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="Обновляет чейнджлог Sunrise из событий GitHub Actions.")
    parser.add_argument("--event-path", type=Path, required=True)
    parser.add_argument("--pr-number", type=int)
    parser.add_argument("--manual-changelog", action="store_true")
    parser.add_argument("--validate-only", action="store_true")
    parser.add_argument("--target-branch", default="master")
    parser.add_argument("--extra-category", action="append", default=[])
    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parents[3]
    if args.validate_only:
        validate_pull_request_event(repo_root, args.event_path)
        return
    if not (repo_root / CHANGELOG_FILE).is_file():
        raise RuntimeError(f"Файл чейнжлога не найден: {CHANGELOG_FILE}")

    category_files = CATEGORY_FILES.copy()
    configured_categories = [
        category.strip()
        for category in os.environ.get("CHANGELOG_EXTRA_CATEGORIES", "").split(",")
        if category.strip()
    ]
    configured_categories.extend(args.extra_category)
    for category in configured_categories:
        if re.fullmatch(r"[A-Za-z]+", category) is None:
            raise RuntimeError(f"Недопустимое имя категории чейнджлога: {category}")
        filename = f"{category}.yml"
        if not (repo_root / CHANGELOG_PATH / filename).exists():
            raise RuntimeError(f"Для категории {category} отсутствует {filename}")
        category_files[category] = filename

    if args.manual_changelog:
        manual_changelog = os.environ.get("MANUAL_CHANGELOG", "")
        written = write_manual_parts(repo_root, manual_changelog, category_files)
    else:
        checkpoint = load_checkpoint(repo_root, category_files)
        pull_requests = list_merged_pull_requests(checkpoint)

        explicit = load_pull_request(args.pr_number) if args.pr_number else load_event_pull_request(args.event_path)
        current_pull_request_number = int(explicit["number"]) if explicit is not None else None
        by_number = {int(item["number"]): item for item in pull_requests}
        if explicit is not None:
            by_number[int(explicit["number"])] = explicit

        written = write_pull_request_parts(
            repo_root,
            list(by_number.values()),
            args.target_branch,
            category_files,
            current_pull_request_number=current_pull_request_number,
        )
    reports = update_changelogs(repo_root, category_files)
    write_commit_message(reports)

    if written:
        report_status("success", f"Чейнджлог обновлён: подготовлено фрагментов — {written}.")
    else:
        report_status("success", "Сверка завершена: новых фрагментов для чейнжлога нет.")


def run() -> None:
    try:
        main()
    except Exception as error:
        report_status("error", f"Чейнджлог завершился с ошибкой: {error}")
        raise


if __name__ == "__main__":
    run()
