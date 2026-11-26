"""Configuration and safe path resolution for ss13-dm-mcp."""

from __future__ import annotations

import json
import os
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path


@dataclass(slots=True)
class RepoConfig:
    """Configured SS13 build repositories."""

    default_name: str | None
    default_path: Path | None
    repos: dict[str, Path]
    _lower_map: dict[str, str]

    def __init__(
        self,
        default_name: str | None,
        repos: dict[str, Path],
        default_path: Path | None = None,
    ) -> None:
        self.default_name = default_name
        self.default_path = default_path
        self.repos = dict(repos)
        self._lower_map = {k.lower(): k for k in self.repos}

    def resolve(self, repo: str | None = None) -> tuple[str, Path]:
        """Resolve a repository name (or default) to (canonical_name, root_path)."""
        if not self.repos and self.default_path is None:
            raise ValueError(
                "No SS13 repository configured. Set SS13_REPO_PATH or SS13_REPOS."
            )

        if repo is None or not repo.strip():
            if self.default_name is not None and self.default_name in self.repos:
                key = self.default_name
                root = self.repos[key]
            elif self.default_path is not None:
                key = "default"
                root = self.default_path
            else:
                key = next(iter(self.repos))
                root = self.repos[key]
        else:
            req = repo.strip()
            if req in self.repos:
                key = req
                root = self.repos[key]
            elif req.lower() in self._lower_map:
                key = self._lower_map[req.lower()]
                root = self.repos[key]
            elif req.lower() == "default":
                if self.default_name is not None and self.default_name in self.repos:
                    key = self.default_name
                    root = self.repos[key]
                elif self.default_path is not None:
                    key = "default"
                    root = self.default_path
                else:
                    available = ", ".join(sorted(self.repos.keys()))
                    raise ValueError(
                        f"Unknown SS13 repository '{req}'. Configured repos: [{available}]."
                    )
            else:
                available = ", ".join(sorted(self.repos.keys()))
                raise ValueError(
                    f"Unknown SS13 repository '{req}'. Configured repos: [{available}]."
                )

        if not root.is_dir():
            raise ValueError(
                f"Configured SS13 repository '{key}' path does not exist or is not a directory: {root}"
            )
        return key, root


def load_repo_config(env: Mapping[str, str] | None = None) -> RepoConfig:
    """Load SS13_REPO_PATH and optional SS13_REPOS from environment variables."""
    source = os.environ if env is None else env
    repos: dict[str, Path] = {}

    raw_repos = source.get("SS13_REPOS", "").strip()
    if raw_repos:
        try:
            parsed = json.loads(raw_repos)
        except json.JSONDecodeError as exc:
            raise ValueError(f"Invalid JSON in SS13_REPOS: {exc}") from exc

        if not isinstance(parsed, dict):
            raise ValueError("SS13_REPOS must be a JSON object mapping repo names to paths.")

        for name, path_val in parsed.items():
            if not isinstance(name, str) or not name.strip():
                raise ValueError("SS13_REPOS keys must be non-empty strings.")
            if not isinstance(path_val, str) or not path_val.strip():
                raise ValueError(f"SS13_REPOS['{name}'] must be a non-empty path string.")
            repos[name.strip()] = Path(path_val.strip()).expanduser().resolve()

    default_name: str | None = None
    default_path: Path | None = None
    raw_default = source.get("SS13_REPO_PATH", "").strip()
    if raw_default:
        resolved_default = Path(raw_default).expanduser().resolve()
        matched_name: str | None = None
        for name, existing_path in repos.items():
            if existing_path == resolved_default:
                matched_name = name
                break
        if matched_name is not None:
            default_name = matched_name
        elif "default" not in repos:
            repos["default"] = resolved_default
            default_name = "default"
        else:
            # Preserve explicit SS13_REPOS["default"]; keep SS13_REPO_PATH as unnamed default_path
            default_path = resolved_default
    elif repos:
        default_name = "default" if "default" in repos else next(iter(repos))

    return RepoConfig(default_name=default_name, repos=repos, default_path=default_path)


def to_posix_rel(path: Path, repo_root: Path) -> str:
    """Return a repository-relative POSIX path."""
    try:
        rel = path.resolve().relative_to(repo_root.resolve())
        return rel.as_posix()
    except ValueError:
        return path.as_posix()


def safe_resolve_under_repo(repo_root: Path, rel_path: str) -> Path:
    """Resolve a relative path safely inside repo_root, rejecting '..' escapes."""
    cleaned = rel_path.strip().replace("\\", "/").lstrip("/")
    if not cleaned:
        raise ValueError("Path must not be empty.")
    root_resolved = repo_root.resolve()
    candidate = (root_resolved / cleaned).resolve()
    try:
        candidate.relative_to(root_resolved)
    except ValueError as exc:
        raise ValueError(f"Path '{rel_path}' escapes repository root.") from exc
    return candidate
