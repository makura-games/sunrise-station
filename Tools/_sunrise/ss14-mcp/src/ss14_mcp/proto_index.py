"""Incremental mtime-cached index of SS14 YAML prototypes."""

from __future__ import annotations

import os
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from .config import to_posix_rel
from .yaml_loader import load_yaml_entries_with_lines


@dataclass(slots=True)
class PrototypeRecord:
    """Single prototype or partial definition parsed from a YAML file."""

    proto_type: str
    proto_id: str
    file: str
    line: int
    parents: list[str]
    abstract: bool
    is_partial: bool
    raw: dict[str, Any]


def _normalize_parents(raw_parent: Any) -> list[str]:
    if raw_parent is None:
        return []
    if isinstance(raw_parent, str):
        val = raw_parent.strip()
        return [val] if val else []
    if isinstance(raw_parent, list):
        out: list[str] = []
        for item in raw_parent:
            if isinstance(item, str) and item.strip():
                out.append(item.strip())
        return out
    return []


def _scan_yaml_files(root_dir: Path) -> dict[Path, tuple[int, int]]:
    """Fast recursive scan of .yml/.yaml files returning {Path: (mtime_ns, size)}."""
    found: dict[Path, tuple[int, int]] = {}
    if not root_dir.is_dir():
        return found

    stack: list[Path] = [root_dir]
    while stack:
        current = stack.pop()
        try:
            with os.scandir(current) as it:
                for entry in it:
                    if entry.is_dir(follow_symlinks=False):
                        stack.append(Path(entry.path))
                    elif entry.is_file(follow_symlinks=False):
                        name = entry.name
                        if name.endswith(".yml") or name.endswith(".yaml"):
                            st = entry.stat(follow_symlinks=False)
                            found[Path(entry.path)] = (st.st_mtime_ns, st.st_size)
        except OSError:
            continue
    return found


class ProtoIndex:
    """In-memory prototype index with incremental mtime refresh."""

    def __init__(self, repo_root: Path) -> None:
        self.repo_root = repo_root
        self.prototypes_dir = repo_root / "Resources" / "Prototypes"
        self._file_stats: dict[str, tuple[int, int]] = {}
        self._file_records: dict[str, list[PrototypeRecord]] = {}
        self.definitions: dict[tuple[str, str], PrototypeRecord] = {}
        self.partials: dict[tuple[str, str], list[PrototypeRecord]] = defaultdict(list)
        self.by_id: dict[str, list[PrototypeRecord]] = defaultdict(list)
        self.children_map: dict[tuple[str, str], list[str]] = defaultdict(list)
        self.parse_errors: dict[str, str] = {}
        self.generation: int = 0
        self._initialized: bool = False

    def ensure_fresh(self) -> bool:
        """Refresh changed/added/deleted YAML files. Returns True if index changed."""
        current_files = _scan_yaml_files(self.prototypes_dir)
        current_by_rel: dict[str, tuple[Path, tuple[int, int]]] = {}
        for path, stat_sig in current_files.items():
            rel = to_posix_rel(path, self.repo_root)
            current_by_rel[rel] = (path, stat_sig)

        removed = set(self._file_stats.keys()) - set(current_by_rel.keys())
        changed: list[tuple[str, Path, tuple[int, int]]] = []
        for rel, (path, stat_sig) in current_by_rel.items():
            if self._file_stats.get(rel) != stat_sig:
                changed.append((rel, path, stat_sig))

        if self._initialized and not removed and not changed:
            return False

        for rel in removed:
            self._file_stats.pop(rel, None)
            self._file_records.pop(rel, None)
            self.parse_errors.pop(rel, None)

        for rel, path, stat_sig in changed:
            self._file_stats[rel] = stat_sig
            self._parse_file(rel, path)

        self._rebuild_indices()
        self._initialized = True
        self.generation += 1
        return True

    def _parse_file(self, rel_path: str, path: Path) -> None:
        records: list[PrototypeRecord] = []
        is_partial_dir = "_sunrise/partial/" in rel_path.lower()
        try:
            text = path.read_text(encoding="utf-8-sig", errors="replace")
            entries = load_yaml_entries_with_lines(text)
        except Exception as exc:
            self._file_records[rel_path] = []
            self.parse_errors[rel_path] = str(exc)
            return

        self.parse_errors.pop(rel_path, None)
        for line_no, entry in entries:
            if not isinstance(entry, dict):
                continue
            raw_type = entry.get("type")
            is_partial = is_partial_dir
            if isinstance(raw_type, dict) and raw_type.get("__partial_only__"):
                is_partial = True
                proto_type = str(raw_type.get("__value__") or "").strip()
            elif isinstance(raw_type, str):
                proto_type = raw_type.strip()
            else:
                continue

            raw_id = entry.get("id")
            if not isinstance(raw_id, str) or not raw_id.strip():
                continue
            proto_id = raw_id.strip()

            parents = _normalize_parents(entry.get("parent"))
            abstract = bool(entry.get("abstract", False))
            records.append(
                PrototypeRecord(
                    proto_type=proto_type,
                    proto_id=proto_id,
                    file=rel_path,
                    line=line_no,
                    parents=parents,
                    abstract=abstract,
                    is_partial=is_partial,
                    raw=entry,
                )
            )
        self._file_records[rel_path] = records

    def _rebuild_indices(self) -> None:
        definitions: dict[tuple[str, str], PrototypeRecord] = {}
        partials: dict[tuple[str, str], list[PrototypeRecord]] = defaultdict(list)
        by_id: dict[str, list[PrototypeRecord]] = defaultdict(list)
        children_map: dict[tuple[str, str], list[str]] = defaultdict(list)

        for rel_path in sorted(self._file_records.keys()):
            for rec in self._file_records[rel_path]:
                key = (rec.proto_type, rec.proto_id)
                by_id[rec.proto_id].append(rec)
                if rec.is_partial:
                    partials[key].append(rec)
                else:
                    definitions[key] = rec

        for (proto_type, proto_id), rec in definitions.items():
            for parent_id in rec.parents:
                children_map[(proto_type, parent_id)].append(proto_id)

        self.definitions = definitions
        self.partials = partials
        self.by_id = by_id
        self.children_map = children_map
