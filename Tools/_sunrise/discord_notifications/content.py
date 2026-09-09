"""Полный текст и изображения GitHub в пределах размеров Discord."""

import ipaddress
import json
import re
from collections import deque
from html import unescape
from html.parser import HTMLParser
from urllib.parse import quote, urljoin, urlsplit


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


class Images(HTMLParser):
    def __init__(self):
        super().__init__()
        self.sources = []
        self.labels = []

    def handle_starttag(self, tag, attrs):
        if tag == "img":
            source = dict(attrs).get("src")
            if source:
                self.sources.append(source)
                self.labels.append(dict(attrs).get("alt", ""))


def prepare_body(body: str, base: str, limit: int = 3500) -> list[dict]:
    """Сохраняет текст, изображения и их повторы в исходном порядке."""
    code = re.compile(
        r"(?P<hidden><!--[\s\S]*?-->)|"
        r"(?m:^[ \t]{0,3}(?P<fence>`{3,}|~{3,})[^\n]*\n"
        r"[\s\S]*?(?:^[ \t]{0,3}(?P=fence)[ \t]*$|\Z))"
        r"|(?P<ticks>`+)(?!`)[\s\S]*?(?<!`)(?P=ticks)(?!`)"
    )
    spans = []
    position = 0
    for match in code.finditer(body):
        spans.append((body[position : match.start()], False))
        if not match["hidden"]:
            spans.append((match[0], True))
        position = match.end()
    spans.append((body[position:], False))
    definition = (
        r"(?m:^[ \t]{0,3}\[([^\]]+)\]:\s*<?(\S+?)>?"
        r'(?:\s+["\'].*?["\'])?[ \t]*$)'
    )
    references = {}
    for value, protected in spans:
        if not protected:
            references.update(
                (name.casefold(), address)
                for name, address in re.findall(
                    definition, re.sub(r"<!--[\s\S]*?-->", "", value)
                )
            )
    inline = (
        r"!\[(?P<label>(?:\\.|[^\]\\])*)\]\("
        r"(?P<url><[^>\n]+>|(?:\\.|[^\s()\\]|\((?:[^()]|\([^()]*\))*\))+?)"
        r'(?:\s+["\'][^\n]*?["\'])?\s*\)'
    )
    reference = r"!\[(?P<alt>[^\]]+)\](?:\[(?P<ref>[^\]]*)\])?(?![\[(])"
    linked = (
        r"\[(?:"
        + re.sub(r"\(\?P<[^>]+>", "(?:", inline + "|" + reference)
        + r")\]\((?P<target><[^>\n]+>|(?:[^\s()]|\([^()]*\))+)\)"
    )
    token = re.compile(
        r"(?P<escape>\\.)|(?P<comment><!--[\s\S]*?-->)"
        r"|(?P<html><img\b(?:[^>\"']|\"[^\"]*\"|'[^']*')*>)"
        r"|(?P<linked>"
        + linked
        + r")|"
        + inline
        + r"|"
        + reference
        + r"|(?P<break><br\s*/?>)"
        r"|(?P<details></?(?:details|summary)\b[^>]*>)"
        r"|(?P<definition>" + definition + r")",
        re.IGNORECASE,
    )
    components = []
    buffer = []

    def flush_text():
        value = "".join(buffer).strip()
        components.extend(
            {"type": 10, "content": part}
            for part in split_body(value, limit)
            if part
        )
        buffer.clear()

    for value, protected in spans:
        if protected:
            buffer.append(value)
            continue
        position = 0
        for match in token.finditer(value):
            buffer.append(value[position : match.start()])
            position = match.end()
            address = None
            label = ""
            if match["linked"]:
                inner = re.search(inline + "|" + reference, match["linked"])
                label = inner["label"] or inner["alt"] or ""
                address = inner["url"] or references.get(
                    (inner["ref"] or inner["alt"]).casefold()
                )
            elif match["url"]:
                address = match["url"].strip("<>")
                label = match["label"]
            elif match["alt"]:
                address = references.get(
                    (match["ref"] or match["alt"]).casefold()
                )
                label = match["alt"]
            elif match["html"]:
                parser = Images()
                parser.feed(match[0])
                address = parser.sources[0] if parser.sources else None
                label = parser.labels[0] if parser.labels else ""
            elif match["break"] or match["details"]:
                buffer.append("\n")
                continue
            elif match["comment"] or match["definition"]:
                continue
            if address is None:
                buffer.append(match[0])
                continue
            address = re.sub(r"\\([\\()])", r"\1", address.strip("<>"))
            url = media_url(address, base)
            if not url:
                buffer.append(label or "Изображение недоступно")
                continue
            flush_text()
            components.append(
                {
                    "type": 12,
                    "items": [
                        {
                            "media": {"url": url},
                            **({"description": label[:1024]} if label else {}),
                        }
                    ],
                }
            )
            if match["linked"]:
                target = media_url(match["target"].strip("<>"), base)
                if target and target != url:
                    buffer.append(f"[Источник изображения](<{target}>)\n")
        buffer.append(value[position:])
    flush_text()
    return components


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


def walk_components(components: list[dict]):
    """Обходит контейнеры и секции в порядке отображения."""
    for component in components:
        yield component
        yield from walk_components(component.get("components", []))
        if component.get("accessory"):
            yield from walk_components([component["accessory"]])


def split_message(message: dict) -> list[dict]:
    """Делит V2-контейнеры, не меняя порядок содержимого."""
    result = []
    for container in message["components"]:
        pending = deque(container["components"])
        batch = []
        while pending:
            component = pending.popleft()
            candidate = {**container, "components": [*batch, component]}
            nodes = list(walk_components([candidate]))
            length = sum(units(node.get("content", "")) for node in nodes)
            size = len(json.dumps(candidate, ensure_ascii=False).encode())
            if len(nodes) <= 40 and length <= 4000 and size <= 9000:
                batch.append(component)
                continue
            if batch:
                result.append(
                    {
                        **message,
                        "components": [{**container, "components": batch}],
                    }
                )
                batch = []
                pending.appendleft(component)
            elif component["type"] == 10 and units(component["content"]) > 128:
                pieces = split_body(
                    component["content"], units(component["content"]) // 2
                )
                pending.extendleft(
                    {**component, "content": piece}
                    for piece in reversed(pieces)
                )
            else:
                raise ValueError(
                    "Один блок сообщения превышает лимиты Discord"
                )
        if batch:
            result.append(
                {
                    **message,
                    "components": [{**container, "components": batch}],
                }
            )
    return result


def body_base(repository: str, subject: dict) -> str:
    revision = subject.get("head", {}).get("sha") or "HEAD"
    return f"https://github.com/{repository}/blob/{quote(revision, safe='')}/"
