"""Read-only upstream hygiene auditor checking git diff outside _Sunrise and AGENTS.md markers."""

from __future__ import annotations

import re
import subprocess
from pathlib import Path
from typing import Any

_INHERITED_MODULES = {
    "_starlight",
    "_rmc14",
    "_fish",
    "_afterlight",
    "_farhorizons",
}

_MARKER_REQUIRED_EXTS = {".cs", ".yml", ".yaml", ".ftl", ".xaml"}

_MARKER_LINE_RE = re.compile(
    r"(?://|#)\s*Sunrise[- ]Edit\b|<!--\s*Sunrise[- ]Edit\b",
    re.IGNORECASE,
)
_MARKER_START_RE = re.compile(
    r"(?://|#)\s*Sunrise\s+(?:edit|added)\s+start\b",
    re.IGNORECASE,
)
_MARKER_END_RE = re.compile(
    r"(?://|#)\s*Sunrise\s+(?:edit|added)\s+end\b",
    re.IGNORECASE,
)
_LEGACY_START_RE = re.compile(r"(?://|#)\s*Sunrise-Start\b", re.IGNORECASE)
_LEGACY_END_RE = re.compile(r"(?://|#)\s*Sunrise-End\b", re.IGNORECASE)


def _run_git(repo_root: Path, args: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args],
        cwd=repo_root,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )


def _classify_path(rel_path: str) -> str:
    parts = {p.lower() for p in rel_path.replace("\\", "/").split("/") if p}
    if "_sunrise" in parts:
        return "sunrise"
    if parts & _INHERITED_MODULES:
        return "inherited_module"
    return "vanilla"


def _scan_markers(text: str) -> dict[str, Any]:
    lines = text.splitlines()
    inline_count = 0
    start_count = 0
    end_count = 0
    legacy_start = 0
    legacy_end = 0

    for line in lines:
        if _MARKER_LINE_RE.search(line):
            inline_count += 1
        if _MARKER_START_RE.search(line):
            start_count += 1
        if _MARKER_END_RE.search(line):
            end_count += 1
        if _LEGACY_START_RE.search(line):
            legacy_start += 1
        if _LEGACY_END_RE.search(line):
            legacy_end += 1

    has_standard_marker = (inline_count > 0) or (start_count > 0)
    has_legacy_marker = legacy_start > 0
    unclosed_blocks = max(0, start_count - end_count) + max(
        0, legacy_start - legacy_end
    )

    return {
        "has_marker": has_standard_marker or has_legacy_marker,
        "has_standard_marker": has_standard_marker,
        "has_legacy_marker": has_legacy_marker,
        "inline_markers": inline_count,
        "block_starts": start_count,
        "block_ends": end_count,
        "unclosed_blocks": unclosed_blocks,
    }


def _extract_added_diff_lines(
    repo_root: Path, rel_path: str, base_ref: str | None
) -> str:
    if base_ref:
        res = _run_git(repo_root, ["diff", "--unified=0", base_ref, "--", rel_path])
        text = res.stdout
    else:
        res_u = _run_git(repo_root, ["diff", "--unified=0", "--", rel_path])
        res_s = _run_git(
            repo_root, ["diff", "--cached", "--unified=0", "--", rel_path]
        )
        text = f"{res_u.stdout}\n{res_s.stdout}"

    added: list[str] = []
    for line in text.splitlines():
        if line.startswith("+++") or line.startswith("---"):
            continue
        if line.startswith("+"):
            added.append(line[1:])
    return "\n".join(added)


def check_upstream_hygiene(
    repo_root: Path, base_ref: str | None = None
) -> dict[str, Any]:
    """Audit git working tree / diff against base_ref for edits outside _Sunrise and AGENTS.md markers."""
    check_git = _run_git(repo_root, ["rev-parse", "--is-inside-work-tree"])
    if check_git.returncode != 0:
        return {
            "error": f"Not a git repository: {check_git.stderr.strip() or repo_root}"
        }

    changed: list[tuple[str, str]] = []  # (status, rel_path)

    if base_ref:
        verify = _run_git(repo_root, ["rev-parse", "--verify", base_ref])
        if verify.returncode != 0:
            return {
                "error": f"Invalid base_ref '{base_ref}': {verify.stderr.strip()}"
            }
        diff_res = _run_git(repo_root, ["diff", "--name-status", "-z", base_ref])
        tokens = [t for t in diff_res.stdout.split("\0") if t]
        idx = 0
        while idx < len(tokens):
            status = tokens[idx]
            if status.startswith(("R", "C")) and idx + 2 < len(tokens):
                changed.append((status, tokens[idx + 2]))
                idx += 3
            elif idx + 1 < len(tokens):
                changed.append((status, tokens[idx + 1]))
                idx += 2
            else:
                break

        untracked = _run_git(
            repo_root, ["ls-files", "--others", "--exclude-standard", "-z"]
        )
        for rel in untracked.stdout.split("\0"):
            if rel:
                changed.append(("??", rel))
    else:
        st_res = _run_git(repo_root, ["status", "--porcelain=v1", "-z"])
        entries = st_res.stdout.split("\0")
        i = 0
        while i < len(entries):
            entry = entries[i]
            if not entry or len(entry) < 4:
                i += 1
                continue
            status = entry[:2].strip() or "M"
            rel_path = entry[3:]
            if status.startswith("R") and i + 1 < len(entries):
                rel_path = entries[i + 1]
                i += 2
            else:
                i += 1
            changed.append((status, rel_path))

    counts = {
        "sunrise": 0,
        "inherited_module": 0,
        "vanilla": 0,
        "unmarked_outside_sunrise": 0,
        "unclosed_marker_blocks": 0,
        "untracked_other": 0,
    }
    outside_details: list[dict[str, Any]] = []
    untracked_other: list[str] = []

    for status, raw_rel in changed:
        rel_path = raw_rel.replace("\\", "/")
        category = _classify_path(rel_path)
        counts[category] += 1

        if category == "sunrise":
            continue

        ext = Path(rel_path).suffix.lower()
        is_code_or_content = ext in _MARKER_REQUIRED_EXTS

        if not is_code_or_content:
            if status == "??":
                counts["untracked_other"] += 1
                untracked_other.append(rel_path)
            else:
                outside_details.append(
                    {
                        "path": rel_path,
                        "status": status,
                        "category": category,
                        "marker_required": False,
                    }
                )
            continue

        is_deleted = status.startswith("D")
        marker_info: dict[str, Any] = {
            "has_marker": True,
            "has_standard_marker": True,
            "has_legacy_marker": False,
            "unclosed_blocks": 0,
        }
        if not is_deleted:
            if status == "??" or status.startswith("A"):
                file_path = repo_root / rel_path
                try:
                    content = file_path.read_text(
                        encoding="utf-8-sig", errors="replace"
                    )
                except OSError:
                    content = ""
                marker_info = _scan_markers(content)
            else:
                diff_added = _extract_added_diff_lines(repo_root, rel_path, base_ref)
                marker_info = _scan_markers(diff_added)

            if not marker_info["has_marker"]:
                counts["unmarked_outside_sunrise"] += 1
            if marker_info["unclosed_blocks"] > 0:
                counts["unclosed_marker_blocks"] += 1

        outside_details.append(
            {
                "path": rel_path,
                "status": status,
                "category": category,
                "deleted": is_deleted,
                "marker_required": True,
                **marker_info,
            }
        )

    ok = (
        counts["unmarked_outside_sunrise"] == 0
        and counts["unclosed_marker_blocks"] == 0
    )

    result: dict[str, Any] = {
        "ok": ok,
        "base_ref": base_ref,
        "total_changed": len(changed),
        "counts": counts,
        "outside_sunrise_truncated": len(outside_details) > 50,
        "outside_sunrise_files": outside_details[:50],
    }
    if untracked_other:
        result["untracked_other"] = untracked_other[:50]
    return result
