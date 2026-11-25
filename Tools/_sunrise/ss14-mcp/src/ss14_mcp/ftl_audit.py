"""FTL localization parity, prototype ent-* coverage, and C# Loc.GetString auditor."""

from __future__ import annotations

import os
import re
from pathlib import Path
from typing import Any

from .config import resolve_case_insensitive_path, to_posix_rel
from .proto_index import ProtoIndex

_FTL_KEY_RE = re.compile(r"^([A-Za-z0-9_-]+)\s*=", re.MULTILINE)
_LOC_CALL_RE = re.compile(
    r'\bLoc\.(?:Try)?GetString\s*\(\s*"([A-Za-z0-9_.-]+)"'
)
_SKIP_CS_DIRS = {"bin", "obj", ".git", ".vs", "node_modules"}
_NORM_SEP_RE = re.compile(r"[-_\s]+")


def _norm_token(text: str) -> str:
    return _NORM_SEP_RE.sub("", text.lower())


def _strip_locale_root(rel_file: str, locale: str) -> str:
    prefix = f"resources/locale/{locale.lower()}/"
    if rel_file.lower().startswith(prefix):
        return rel_file[len(prefix) :]
    return rel_file


def _scan_ftl_files(root_dir: Path) -> dict[Path, tuple[int, int]]:
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
                    elif entry.is_file(follow_symlinks=False) and entry.name.endswith(".ftl"):
                        st = entry.stat(follow_symlinks=False)
                        found[Path(entry.path)] = (st.st_mtime_ns, st.st_size)
        except OSError:
            continue
    return found


class FtlIndex:
    """Incremental mtime-cached index of Fluent (.ftl) keys for ru-RU and en-US."""

    def __init__(self, repo_root: Path) -> None:
        self.repo_root = repo_root
        self._file_stats: dict[str, dict[str, tuple[int, int]]] = {
            "ru-RU": {},
            "en-US": {},
        }
        self._file_keys: dict[str, dict[str, list[str]]] = {
            "ru-RU": {},
            "en-US": {},
        }
        self.all_keys: dict[str, set[str]] = {"ru-RU": set(), "en-US": set()}
        self._initialized = False

    def ensure_fresh(self) -> bool:
        changed_any = False
        for locale in ("ru-RU", "en-US"):
            locale_dir = self.repo_root / "Resources" / "Locale" / locale
            current = _scan_ftl_files(locale_dir)
            by_rel = {
                to_posix_rel(p, self.repo_root): (p, sig)
                for p, sig in current.items()
            }
            stats = self._file_stats[locale]
            file_keys = self._file_keys[locale]

            removed = set(stats.keys()) - set(by_rel.keys())
            changed = [
                (rel, p, sig)
                for rel, (p, sig) in by_rel.items()
                if stats.get(rel) != sig
            ]
            if removed or changed:
                changed_any = True
                for rel in removed:
                    stats.pop(rel, None)
                    file_keys.pop(rel, None)
                for rel, p, sig in changed:
                    stats[rel] = sig
                    try:
                        text = p.read_text(encoding="utf-8-sig", errors="replace")
                        file_keys[rel] = _FTL_KEY_RE.findall(text)
                    except OSError:
                        file_keys[rel] = []

                merged: set[str] = set()
                for keys in file_keys.values():
                    merged.update(keys)
                self.all_keys[locale] = merged

        if not self._initialized:
            self._initialized = True
            return True
        return changed_any

    def keys_under_prefix(
        self, locale: str, resolved_path: Path | None
    ) -> tuple[dict[str, str], list[str]]:
        """Return ({key: first_file}, [duplicate_keys]) for a resolved file or directory."""
        if resolved_path is None or not resolved_path.exists():
            return {}, []

        rel_target = to_posix_rel(resolved_path, self.repo_root).lower()
        is_file = resolved_path.is_file()
        prefix = rel_target if is_file else rel_target.rstrip("/") + "/"

        found: dict[str, str] = {}
        duplicates: list[str] = []
        for rel_file, keys in sorted(self._file_keys[locale].items()):
            rel_lower = rel_file.lower()
            if (is_file and rel_lower == prefix) or (
                not is_file and rel_lower.startswith(prefix)
            ):
                for key in keys:
                    if key in found:
                        duplicates.append(f"{key} ({rel_file})")
                    else:
                        found[key] = rel_file
        return found, duplicates

    def keys_matching_query(
        self, locale: str, query: str
    ) -> tuple[dict[str, str], list[str], list[str], set[str]]:
        """Match FTL files whose locale-relative path matches query (case-insensitive, -/_ normalized)."""
        needle = _norm_token(query)
        found: dict[str, str] = {}
        duplicates: list[str] = []
        matched_files: list[str] = []
        matched_dirs: set[str] = set()

        if not needle:
            return found, duplicates, matched_files, matched_dirs

        for rel_file, keys in sorted(self._file_keys[locale].items()):
            subpath = _strip_locale_root(rel_file, locale)
            if needle in _norm_token(subpath):
                matched_files.append(rel_file)
                parent_dir = str(Path(subpath).parent.as_posix())
                matched_dirs.add(subpath if parent_dir == "." else parent_dir)
                for key in keys:
                    if key in found:
                        duplicates.append(f"{key} ({rel_file})")
                    else:
                        found[key] = rel_file

        return found, duplicates, matched_files, matched_dirs

    def find_matching_dirs(self, hint: str, limit: int = 20) -> list[str]:
        """Find locale-relative directories matching hint (case-insensitive, -/_ normalized)."""
        needle = _norm_token(hint)
        if not needle:
            return []
        dirs: set[str] = set()
        for locale in ("ru-RU", "en-US"):
            for rel_file in self._file_keys[locale]:
                subpath = _strip_locale_root(rel_file, locale)
                if needle in _norm_token(subpath):
                    parent_dir = str(Path(subpath).parent.as_posix())
                    dirs.add(subpath if parent_dir == "." else parent_dir)
        return sorted(dirs)[:limit]


def _collect_cs_loc_keys(
    repo_root: Path, cs_path_prefix: str
) -> tuple[Path | None, dict[str, list[str]]]:
    cleaned = cs_path_prefix.strip().replace("\\", "/").strip("/")
    target = resolve_case_insensitive_path(repo_root, cleaned)
    if target is None or not target.exists():
        return None, {}

    cs_files: list[Path] = []
    if target.is_file():
        if target.name.endswith(".cs"):
            cs_files.append(target)
    else:
        stack: list[Path] = [target]
        while stack:
            cur = stack.pop()
            try:
                with os.scandir(cur) as it:
                    for entry in it:
                        if entry.is_dir(follow_symlinks=False):
                            if entry.name not in _SKIP_CS_DIRS:
                                stack.append(Path(entry.path))
                        elif entry.is_file(follow_symlinks=False) and entry.name.endswith(".cs"):
                            cs_files.append(Path(entry.path))
            except OSError:
                continue

    key_locations: dict[str, list[str]] = {}
    for cs_file in sorted(cs_files):
        rel = to_posix_rel(cs_file, repo_root)
        try:
            text = cs_file.read_text(encoding="utf-8-sig", errors="replace")
        except OSError:
            continue
        for idx, line in enumerate(text.splitlines(), start=1):
            for match in _LOC_CALL_RE.finditer(line):
                loc_key = match.group(1)
                key_locations.setdefault(loc_key, []).append(f"{rel}:{idx}")
    return target, key_locations


def audit_ftl(
    repo_root: Path,
    proto_index: ProtoIndex,
    ftl_index: FtlIndex,
    path_prefix: str | None = None,
    query: str | None = None,
    proto_path_prefix: str | None = None,
    cs_path_prefix: str | None = None,
    limit: int = 50,
) -> dict[str, Any]:
    """Audit ru-RU <-> en-US FTL parity, prototype ent-* keys, and C# Loc.GetString keys."""
    ftl_index.ensure_fresh()
    clamped_limit = max(1, min(limit, 200))

    if query and query.strip():
        q_str = query.strip()
        ru_keys, ru_dups, ru_files, ru_dirs = ftl_index.keys_matching_query("ru-RU", q_str)
        en_keys, en_dups, en_files, en_dirs = ftl_index.keys_matching_query("en-US", q_str)
        all_dirs = sorted(ru_dirs | en_dirs)
        if not ru_files and not en_files:
            return {
                "error": f"No FTL files matched query '{q_str}'.",
                "query": q_str,
                "matched_dirs": [],
            }
        result: dict[str, Any] = {
            "query": q_str,
            "matched_dirs": all_dirs[:clamped_limit],
            "matched_files_count": {"ru": len(ru_files), "en": len(en_files)},
        }
    elif path_prefix and path_prefix.strip():
        cleaned_prefix = path_prefix.strip().replace("\\", "/").strip("/")
        for strip_p in ("Resources/Locale/ru-RU/", "Resources/Locale/en-US/"):
            if cleaned_prefix.lower().startswith(strip_p.lower()):
                cleaned_prefix = cleaned_prefix[len(strip_p) :]
                break

        ru_base = repo_root / "Resources" / "Locale" / "ru-RU"
        en_base = repo_root / "Resources" / "Locale" / "en-US"
        ru_target = resolve_case_insensitive_path(ru_base, cleaned_prefix)
        en_target = resolve_case_insensitive_path(en_base, cleaned_prefix)

        if ru_target is None and en_target is None:
            hint = cleaned_prefix.split("/")[-1]
            return {
                "error": f"Locale path '{path_prefix}' not found under Resources/Locale/ru-RU or en-US.",
                "path_prefix": path_prefix,
                "matched_dirs": ftl_index.find_matching_dirs(hint),
            }

        ru_keys, ru_dups = ftl_index.keys_under_prefix("ru-RU", ru_target)
        en_keys, en_dups = ftl_index.keys_under_prefix("en-US", en_target)
        result = {
            "path_prefix": cleaned_prefix,
            "ru_path": to_posix_rel(ru_target, repo_root) if ru_target else None,
            "en_path": to_posix_rel(en_target, repo_root) if en_target else None,
        }
        if not ru_keys and not en_keys:
            hint = cleaned_prefix.split("/")[-1]
            result["matched_dirs"] = ftl_index.find_matching_dirs(hint)
    else:
        return {"error": "Either path_prefix or query must be provided."}

    missing_in_ru = sorted(set(en_keys.keys()) - set(ru_keys.keys()))
    missing_in_en = sorted(set(ru_keys.keys()) - set(en_keys.keys()))

    result["counts"] = {
        "ru_keys": len(ru_keys),
        "en_keys": len(en_keys),
        "missing_in_ru": len(missing_in_ru),
        "missing_in_en": len(missing_in_en),
        "duplicate_keys_ru": len(ru_dups),
        "duplicate_keys_en": len(en_dups),
    }
    result["missing_in_ru"] = [
        {"key": k, "en_file": en_keys[k]} for k in missing_in_ru[:clamped_limit]
    ]
    result["missing_in_en"] = [
        {"key": k, "ru_file": ru_keys[k]} for k in missing_in_en[:clamped_limit]
    ]
    if ru_dups:
        result["duplicate_keys_ru"] = ru_dups[:clamped_limit]
    if en_dups:
        result["duplicate_keys_en"] = en_dups[:clamped_limit]

    if proto_path_prefix:
        proto_index.ensure_fresh()
        norm_proto = proto_path_prefix.strip().replace("\\", "/").strip("/")
        if not norm_proto.lower().startswith("resources/prototypes"):
            norm_proto = f"Resources/Prototypes/{norm_proto}"
        norm_proto_lower = norm_proto.lower()

        missing_proto_ru: list[dict[str, Any]] = []
        missing_proto_en: list[dict[str, Any]] = []
        checked_protos = 0

        for (ptype, pid), rec in sorted(proto_index.definitions.items()):
            if ptype != "entity" or rec.abstract:
                continue
            if not rec.file.lower().startswith(norm_proto_lower):
                continue
            checked_protos += 1
            ent_key = f"ent-{pid}"
            if ent_key not in ftl_index.all_keys["ru-RU"]:
                missing_proto_ru.append(
                    {"id": pid, "key": ent_key, "source": f"{rec.file}:{rec.line}"}
                )
            if ent_key not in ftl_index.all_keys["en-US"]:
                missing_proto_en.append(
                    {
                        "id": pid,
                        "key": ent_key,
                        "has_yaml_name": "name" in rec.raw,
                        "source": f"{rec.file}:{rec.line}",
                    }
                )

        result["prototype_check"] = {
            "proto_path_prefix": norm_proto,
            "checked_entities": checked_protos,
            "missing_ent_in_ru_count": len(missing_proto_ru),
            "missing_ent_in_en_count": len(missing_proto_en),
            "missing_ent_in_ru": missing_proto_ru[:clamped_limit],
            "missing_ent_in_en": missing_proto_en[:clamped_limit],
        }

    if cs_path_prefix:
        cs_target, loc_calls = _collect_cs_loc_keys(repo_root, cs_path_prefix)
        if cs_target is None:
            result["cs_check"] = {
                "error": f"C# path '{cs_path_prefix}' not found."
            }
        else:
            missing_cs_ru: list[dict[str, Any]] = []
            missing_cs_en: list[dict[str, Any]] = []
            for loc_key, locs in sorted(loc_calls.items()):
                if loc_key not in ftl_index.all_keys["ru-RU"]:
                    missing_cs_ru.append({"key": loc_key, "locations": locs[:5]})
                if loc_key not in ftl_index.all_keys["en-US"]:
                    missing_cs_en.append({"key": loc_key, "locations": locs[:5]})

            result["cs_check"] = {
                "cs_path_prefix": to_posix_rel(cs_target, repo_root),
                "keys_referenced": len(loc_calls),
                "missing_in_ru_count": len(missing_cs_ru),
                "missing_in_en_count": len(missing_cs_en),
                "missing_in_ru": missing_cs_ru[:clamped_limit],
                "missing_in_en": missing_cs_en[:clamped_limit],
            }

    return result
