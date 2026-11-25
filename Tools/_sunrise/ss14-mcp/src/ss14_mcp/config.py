"""Zero-config repository root discovery and cross-platform path helpers."""

from __future__ import annotations

import os
from pathlib import Path

ENV_REPO_PATH = "SS14_REPO_PATH"


def is_ss14_repo_root(path: Path) -> bool:
    """Return True if path looks like a Space Station 14 repository root."""
    return (path / "Resources" / "Prototypes").is_dir() and (path / "Content.Server").is_dir()


def find_repo_root(start_path: Path | None = None) -> Path:
    """Resolve the repository root from SS14_REPO_PATH, explicit start_path, CWD, or __file__."""
    env_override = os.environ.get(ENV_REPO_PATH, "").strip()
    if env_override:
        candidate = Path(env_override).expanduser().resolve()
        if not is_ss14_repo_root(candidate):
            raise FileNotFoundError(
                f"{ENV_REPO_PATH}='{env_override}' is not a valid SS14 repository root "
                "(expected Resources/Prototypes and Content.Server)."
            )
        return candidate

    roots_to_try = (
        [start_path] if start_path is not None else [Path.cwd(), Path(__file__)]
    )
    for root_hint in roots_to_try:
        current = root_hint.resolve()
        if current.is_file():
            current = current.parent
        for parent in (current, *current.parents):
            if is_ss14_repo_root(parent):
                return parent

    raise FileNotFoundError(
        "Could not auto-detect SS14 repository root containing Resources/Prototypes and Content.Server. "
        f"Set {ENV_REPO_PATH} to override."
    )


def to_posix_rel(path: Path, repo_root: Path) -> str:
    """Convert a path under repo_root to a normalized POSIX relative path."""
    try:
        return path.relative_to(repo_root).as_posix()
    except ValueError:
        try:
            return path.resolve().relative_to(repo_root.resolve()).as_posix()
        except ValueError:
            return path.as_posix()


def safe_resolve_under_repo(repo_root: Path, raw_path: str) -> Path:
    """Resolve a relative or leading-slash path strictly inside repo_root."""
    cleaned = raw_path.strip().replace("\\", "/").lstrip("/")
    root_resolved = repo_root.resolve()
    target = (root_resolved / cleaned).resolve()
    try:
        target.relative_to(root_resolved)
    except ValueError as exc:
        raise ValueError(f"Path '{raw_path}' escapes repository root.") from exc
    return target


def resolve_case_insensitive_path(base: Path, rel_path: str) -> Path | None:
    """Resolve rel_path under base, falling back to case-insensitive segment matching."""
    cleaned = rel_path.strip().replace("\\", "/").strip("/")
    if not cleaned:
        return base if base.exists() else None

    exact = base / cleaned
    if exact.exists():
        return exact

    current = base
    for part in cleaned.split("/"):
        if not part or part == ".":
            continue
        direct = current / part
        if direct.exists():
            current = direct
            continue
        if not current.is_dir():
            return None
        lower_part = part.lower()
        match: Path | None = None
        try:
            with os.scandir(current) as it:
                for entry in it:
                    if entry.name.lower() == lower_part:
                        match = Path(entry.path)
                        break
        except OSError:
            return None
        if match is None:
            return None
        current = match
    return current
