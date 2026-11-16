#!/usr/bin/env python3

"""Извлекает полезные сведения об ошибках NUnit из консольного лога шарда.

Адаптер NUnit для TRX иногда оставляет в ``ErrorInfo`` только предупреждение
из teardown, если ошибка произошла и в теле интеграционного теста. В консольном
логе при этом остаётся полный блок ошибки, поэтому запускающий скрипт сохраняет
его и выводит после завершения процесса тестов.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path
from typing import Iterable, Sequence


_ANSI_ESCAPE = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")
_FAILURE_HEADER = re.compile(
    r"^\s*(?:Failed|Not passed|Не пройден)\s+\S", re.IGNORECASE
)
_RESULT_BOUNDARY = re.compile(
    r"^\s*(?:Passed|Failed|Skipped|Inconclusive|Not run|Not passed|Пройден|Не пройден)\b",
    re.IGNORECASE,
)
_SUMMARY_LINE = re.compile(
    r"^\s*(?:Test Run|Overall result|Passed!|Failed!)\b", re.IGNORECASE
)

# NUnit выводит эти маркеры для assertions и исключений. Обычная строка ``WARN``
# намеренно исключена: штатные предупреждения сервера не должны считаться
# первичной ошибкой теста.
_DIAGNOSTIC_MARKERS = (
    "Error Message:",
    "Multiple failures",
    "Assert.",
    "Expected:",
    "But was:",
    "Stack Trace:",
    "Unhandled exception",
    "MultipleAssertException",
    "AssertionException",
    "Test was dirty-disposed.",
    "Exception:",
)
_PRIMARY_MARKERS = (
    "Multiple failures",
    "Assert.",
    "Expected:",
    "But was:",
    "Unhandled exception",
    "MultipleAssertException",
    "AssertionException",
    "Exception:",
)
_FALLBACK_MARKERS = tuple(marker for marker in _PRIMARY_MARKERS if marker != "Exception:")


def _is_diagnostic(line: str) -> bool:
    lowered = line.casefold()
    return any(marker.casefold() in lowered for marker in _DIAGNOSTIC_MARKERS)


def _is_failure_header(line: str) -> bool:
    return _FAILURE_HEADER.match(line) is not None


def _is_boundary(line: str) -> bool:
    return _RESULT_BOUNDARY.match(line) is not None or _SUMMARY_LINE.match(line) is not None


def _trim_standard_output(block: Sequence[str]) -> list[str]:
    """Оставляет секции ошибки/стека NUnit и не повторяет мегабайты логов."""

    output_index = next(
        (
            index
            for index, line in enumerate(block)
            if line.strip().casefold() == "standard output messages:"
        ),
        None,
    )
    if output_index is None:
        return list(block)

    diagnostic = list(block[:output_index])
    output = block[output_index:]
    marker_indexes = [index for index, line in enumerate(output) if _is_diagnostic(line)]
    if not marker_indexes:
        return diagnostic

    # Некоторые адаптеры помещают исключение только в standard output. В этом
    # случае оставляем небольшой контекст вокруг каждой диагностической строки.
    selected = set(range(min(marker_indexes) - 2, max(marker_indexes) + 3))
    selected = {index for index in selected if 0 <= index < len(output)}
    output_context = [line for index, line in enumerate(output) if index in selected]
    return diagnostic + output_context


def _fallback_blocks(lines: Sequence[str]) -> list[list[str]]:
    """Восстанавливает диагностику, если консольный логгер не вывел заголовок теста."""

    # Не считаем обычные серверные строки с ``Exception:`` ошибкой теста без
    # явного assertion-маркера: такие предупреждения встречаются и в успешных
    # шардах.
    marker_indexes = [
        index
        for index, line in enumerate(lines)
        if any(marker.casefold() in line.casefold() for marker in _FALLBACK_MARKERS)
    ]
    ranges: list[tuple[int, int]] = []
    for marker_index in marker_indexes:
        start = max(0, marker_index - 3)
        end = min(len(lines), marker_index + 4)
        if ranges and start <= ranges[-1][1]:
            ranges[-1] = (ranges[-1][0], max(ranges[-1][1], end))
        else:
            ranges.append((start, end))

    return [list(lines[start:end]) for start, end in ranges]


def extract_failure_blocks(lines: Iterable[str]) -> list[list[str]]:
    """Извлекает диагностические блоки из обычного консольного вывода NUnit."""

    normalized = [_ANSI_ESCAPE.sub("", line.rstrip("\r\n")) for line in lines]
    starts = [index for index, line in enumerate(normalized) if _is_failure_header(line)]
    blocks: list[list[str]] = []

    for start in starts:
        end = len(normalized)
        for index in range(start + 1, len(normalized)):
            if _is_boundary(normalized[index]):
                end = index
                break

        block = _trim_standard_output(normalized[start:end])
        if block and any(_is_diagnostic(line) for line in block):
            blocks.append(block)

    if blocks:
        return blocks

    return _fallback_blocks(normalized)


def report_failures(log_path: Path, output=None) -> bool:
    """Печатает сведения об ошибках из ``log_path`` и сообщает, были ли они найдены."""

    if output is None:
        output = sys.stdout

    try:
        lines = log_path.read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError as error:
        print(f"Unable to read shard console log {log_path}: {error}", file=sys.stderr)
        return False

    blocks = extract_failure_blocks(lines)
    if not blocks:
        return False

    print(f"\nPrimary failure details from {log_path}:", file=output)
    if not any(
        any(marker.casefold() in line.casefold() for marker in _PRIMARY_MARKERS)
        for block in blocks
        for line in block
    ):
        print(
            "No primary assertion marker was found; inspect the full console log for the primary failure.",
            file=output,
        )

    for number, block in enumerate(blocks, start=1):
        print(f"\n--- failure {number} ---", file=output)
        print("\n".join(block).rstrip(), file=output)

    return True


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("log", type=Path, help="консольный лог NUnit для проверки")
    args = parser.parse_args()
    report_failures(args.log)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
