"""Полный текст и изображения GitHub в пределах размеров Discord."""

import ipaddress
import re
from html import unescape
from html.parser import HTMLParser
from pathlib import PurePosixPath
from urllib.parse import quote, urljoin, urlsplit

CODE_LANGUAGES = {
    ".c": "c",
    ".cpp": "cpp",
    ".cs": "csharp",
    ".csproj": "xml",
    ".css": "css",
    ".ftl": "ini",
    ".go": "go",
    ".h": "cpp",
    ".html": "html",
    ".json": "json",
    ".js": "javascript",
    ".jsx": "javascript",
    ".md": "markdown",
    ".ps1": "powershell",
    ".py": "python",
    ".rs": "rust",
    ".scss": "scss",
    ".sh": "bash",
    ".sql": "sql",
    ".swsl": "glsl",
    ".toml": "toml",
    ".ts": "typescript",
    ".tsx": "typescript",
    ".xaml": "xml",
    ".xml": "xml",
    ".yaml": "yaml",
    ".yml": "yaml",
}


def units(value: str) -> int:
    return len(value.encode("utf-16-le", "replace")) // 2


def media_url(value: str, base: str) -> str:
    try:
        value = urljoin(base, unescape(value))
        parsed = urlsplit(value)
        port = parsed.port
    except ValueError:
        return ""
    if (
        parsed.scheme != "https"
        or not parsed.hostname
        or parsed.username
        or parsed.password
        or port not in {None, 443}
        or re.search(r"[\s<>\x00-\x1f]", value)
        or len(value) > 2048
    ):
        return ""
    try:
        if not ipaddress.ip_address(parsed.hostname).is_global:
            return ""
    except ValueError:
        if "." not in parsed.hostname or parsed.hostname.endswith(".local"):
            return ""
    if parsed.hostname == "github.com" and "/blob/" in parsed.path:
        value = value.replace("github.com/", "raw.githubusercontent.com/", 1)
        value = value.replace("/blob/", "/", 1)
    return value


def add_code_language(body: str, path: object) -> str:
    suffix = PurePosixPath(str(path or "")).suffix.lower()
    language = CODE_LANGUAGES.get(suffix)
    if not language:
        return body
    result = []
    fence = ""
    for line in body.splitlines(keepends=True):
        match = re.match(
            r"^([ \t]*(?:>[ \t]*)*)(`{3,}|~{3,})([^\r\n]*)(\r?\n)?$",
            line,
        )
        if match:
            marker = match.group(2)
            info = match.group(3).strip()
            if fence:
                if (
                    marker[0] == fence[0]
                    and len(marker) >= len(fence)
                    and not info
                ):
                    fence = ""
            else:
                fence = marker
                if not info or info.casefold().startswith("suggestion"):
                    ending = match.group(4) or ""
                    line = f"{match.group(1)}{marker}{language}{ending}"
        result.append(line)
    return "".join(result)


class Images(HTMLParser):
    def __init__(self):
        super().__init__()
        self.sources = []

    def handle_starttag(self, tag, attrs):
        if tag == "img":
            source = dict(attrs).get("src")
            if source:
                self.sources.append(source)


def prepare_body(body: str, base: str) -> tuple[str, list[str]]:
    visible = re.sub(
        r"(```[\s\S]*?```|~~~[\s\S]*?~~~|`[^`\n]+`)|<!--[\s\S]*?-->",
        lambda match: match.group(1) or "",
        body,
    ).strip()
    images = []
    parser = Images()
    references = dict(
        re.findall(
            r'^\s*\[([^\]]+)\]:\s*<?(\S+?)>?(?:\s+".*?")?\s*$',
            visible,
            re.MULTILINE,
        )
    )
    references = {name.casefold(): url for name, url in references.items()}
    parts = re.split(r"(```[\s\S]*?```|~~~[\s\S]*?~~~|`[^`\n]+`)", visible)
    for index in range(0, len(parts), 2):
        part = parts[index]
        parser.feed(part)

        def inline_image(match):
            label, address = match.group(1), match.group(2)
            address = address.strip("<>")
            images.append(address)
            link = media_url(address, base) or address
            return f"[{label or 'Изображение'}]({link})"

        part = re.sub(
            r"!\[([^\]]*)\]\((<[^>]+>|(?:[^\s()]|\([^()]*\))+?)"
            r'(?:\s+["\'][^\n]*?["\'])?\)',
            inline_image,
            part,
        )

        def reference_image(match):
            label = match.group(1)
            address = references.get((match.group(2) or label).casefold())
            if not address:
                return match.group(0)
            images.append(address)
            link = media_url(address, base) or address
            return f"[{label or 'Изображение'}]({link})"

        part = re.sub(
            r"!\[([^\]]+)\](?:\[([^\]]*)\])?(?![\[(])",
            reference_image,
            part,
        )

        def html_image(match):
            image = Images()
            image.feed(match.group(0))
            link = media_url(image.sources[0], base) if image.sources else ""
            return f"[Изображение]({link})" if link else match.group(0)

        part = re.sub(r"<img\b[^>]*>", html_image, part, flags=re.IGNORECASE)
        part = re.sub(r"<br\s*/?>", "\n", part, flags=re.IGNORECASE)
        part = re.sub(
            r"</?(?:details|summary)\b[^>]*>", "\n", part, flags=re.IGNORECASE
        )
        parts[index] = part
    images.extend(parser.sources)
    urls = [media_url(source, base) for source in images]
    visible = "".join(parts).replace("\r\n", "\n").replace("\r", "\n")
    visible = re.sub(r"\n(?:[^\S\n]*\n){2,}", "\n\n", visible).strip()
    return visible, list(dict.fromkeys(url for url in urls if url))


def split_body(body: str, limit: int) -> list[str]:
    chunks = []
    current = ""
    fence = ""
    for line in body.splitlines(keepends=True):
        remaining = line
        while remaining:
            available = limit - units(current) - 16
            encoded = remaining.encode("utf-16-le", "replace")
            piece = encoded[: max(available, 0) * 2].decode(
                "utf-16-le", "ignore"
            )
            if not piece:
                chunks.append(current + ("\n```" if fence else ""))
                current = fence
                continue
            if (
                len(piece) < len(remaining)
                and current != fence
                and units(line) < limit - 32
            ):
                chunks.append(current + ("\n```" if fence else ""))
                current = fence
                continue
            current += piece
            remaining = remaining[len(piece) :]
            if remaining:
                chunks.append(current + ("\n```" if fence else ""))
                current = fence
        marker = re.match(r"^\s*(```|~~~)([^\n]*)", line)
        if marker:
            fence = (
                "" if fence else "```" + marker.group(2).strip()[:30] + "\n"
            )
    if current:
        chunks.append(current + ("\n```" if fence else ""))
    return chunks or [""]


def split_message(message: dict) -> list[dict]:
    result = []
    batch = []
    count = 0
    for embed in message["embeds"]:
        size = sum(
            units(embed.get(key, "")) for key in ("title", "description")
        )
        size += units(embed.get("author", {}).get("name", ""))
        size += units(embed.get("footer", {}).get("text", ""))
        size += sum(
            units(field["name"]) + units(field["value"])
            for field in embed.get("fields", [])
        )
        if batch and (len(batch) == 10 or count + size > 6000):
            result.append({**message, "embeds": batch})
            batch, count = [], 0
        batch.append(embed)
        count += size
    if batch:
        result.append({**message, "embeds": batch})
    for part in result:
        if "_files" in part:
            part["_files"] = {
                name: encoded
                for name, encoded in part["_files"].items()
                if f"attachment://{name}" in str(part["embeds"])
            }
    return result


def body_base(repository: str, subject: dict) -> str:
    revision = subject.get("head", {}).get("sha") or "HEAD"
    return f"https://github.com/{repository}/blob/{quote(revision, safe='')}/"
