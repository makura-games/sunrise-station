"""Incremental mtime-cached DreamMaker index and query engine."""

from __future__ import annotations

import bisect
import os
import re
from collections import defaultdict
from pathlib import Path
from typing import Any

from .config import to_posix_rel
from .dm_parser import (
    DefineDef,
    ParsedDmFile,
    ProcDef,
    ScopeSpan,
    VarDef,
    normalize_typepath,
    parse_dm_file,
)

_SKIP_DIRS = frozenset(
    {
        ".git",
        ".github",
        ".vscode",
        ".venv",
        "__pycache__",
        "node_modules",
        "icons",
        "sound",
        "tgui",
        "rust",
        "html",
        "SQL",
        "data",
        "bin",
        "strings",
    }
)

_BUILTIN_ROOT_PARENTS: dict[str, str] = {
    "/obj": "/atom/movable",
    "/mob": "/atom/movable",
    "/atom/movable": "/atom",
    "/turf": "/atom",
    "/area": "/atom",
    "/atom": "/datum",
    "/client": "/datum",
}


def _scan_dm_files(root_dir: Path) -> dict[str, tuple[Path, tuple[int, int]]]:
    """Fast recursive scan of .dm files returning {posix_rel: (Path, (mtime_ns, size))}."""
    found: dict[str, tuple[Path, tuple[int, int]]] = {}
    if not root_dir.is_dir():
        return found

    stack: list[tuple[Path, str]] = [(root_dir, "")]
    while stack:
        current, prefix = stack.pop()
        try:
            with os.scandir(current) as it:
                for entry in it:
                    name = entry.name
                    if entry.is_dir(follow_symlinks=False):
                        if name not in _SKIP_DIRS:
                            sub_prefix = f"{prefix}/{name}" if prefix else name
                            stack.append((Path(entry.path), sub_prefix))
                    elif entry.is_file(follow_symlinks=False) and name.endswith(".dm"):
                        st = entry.stat(follow_symlinks=False)
                        rel = f"{prefix}/{name}" if prefix else name
                        found[rel] = (Path(entry.path), (st.st_mtime_ns, st.st_size))
        except OSError:
            continue
    return found


def _find_enclosing_scope(scopes: list[ScopeSpan], line_no: int) -> tuple[str | None, str | None]:
    """Return (enclosing_typepath, enclosing_proc) for a 1-based line number."""
    best_type: str | None = None
    best_type_len = 10**9
    best_proc: str | None = None
    best_proc_type: str | None = None
    best_proc_len = 10**9

    for sc in scopes:
        if sc.start_line <= line_no <= sc.end_line:
            span_len = sc.end_line - sc.start_line
            if sc.proc_name is not None:
                if span_len <= best_proc_len:
                    best_proc_len = span_len
                    best_proc = sc.proc_name
                    best_proc_type = sc.typepath
            elif sc.typepath is not None:
                if span_len <= best_type_len:
                    best_type_len = span_len
                    best_type = sc.typepath

    return (best_proc_type or best_type), best_proc


class DmIndex:
    """In-memory DreamMaker index with incremental mtime refresh."""

    def __init__(self, repo_root: Path, repo_name: str = "default") -> None:
        self.repo_root = repo_root
        self.repo_name = repo_name
        self._file_stats: dict[str, tuple[int, int]] = {}
        self._file_parsed: dict[str, ParsedDmFile] = {}
        self._file_texts: dict[str, str] = {}

        self.type_locations: dict[str, list[tuple[str, int]]] = defaultdict(list)
        self.type_vars: dict[str, list[VarDef]] = defaultdict(list)
        self.type_parent_overrides: dict[str, str] = {}
        self.type_procs: dict[str, dict[str, list[ProcDef]]] = defaultdict(lambda: defaultdict(list))
        self.defines_by_name: dict[str, list[DefineDef]] = defaultdict(list)
        self.all_known_types: set[str] = set()

        self.generation: int = 0
        self._initialized: bool = False

    def ensure_fresh(self) -> bool:
        """Refresh added/modified/deleted .dm files. Returns True if index changed."""
        current_by_rel = _scan_dm_files(self.repo_root)

        removed = set(self._file_stats.keys()) - set(current_by_rel.keys())
        changed: list[tuple[str, Path, tuple[int, int]]] = []
        for rel, (path, stat_sig) in current_by_rel.items():
            if self._file_stats.get(rel) != stat_sig:
                changed.append((rel, path, stat_sig))

        if self._initialized and not removed and not changed:
            return False

        for rel in removed:
            self._file_stats.pop(rel, None)
            self._file_parsed.pop(rel, None)
            self._file_texts.pop(rel, None)

        for rel, path, stat_sig in changed:
            self._file_stats[rel] = stat_sig
            text = path.read_text(encoding="utf-8-sig", errors="replace")
            self._file_texts[rel] = text
            self._file_parsed[rel] = parse_dm_file(rel, text)

        self._rebuild_indices()
        self._initialized = True
        self.generation += 1
        return True

    def _rebuild_indices(self) -> None:
        self.type_locations.clear()
        self.type_vars.clear()
        self.type_parent_overrides.clear()
        self.type_procs.clear()
        self.defines_by_name.clear()
        self.all_known_types.clear()

        for rel in sorted(self._file_parsed.keys()):
            parsed = self._file_parsed[rel]

            for tp, lines in parsed.type_locations.items():
                self.type_locations[tp].extend((rel, ln) for ln in lines)
                self._register_type_and_prefixes(tp)

            for tp, vdefs in parsed.vars_by_type.items():
                self.type_vars[tp].extend(vdefs)
                self._register_type_and_prefixes(tp)

            for tp, parent_tp in parsed.parent_types.items():
                self.type_parent_overrides[tp] = parent_tp
                self._register_type_and_prefixes(tp)
                self._register_type_and_prefixes(parent_tp)

            for pdef in parsed.procs:
                self.type_procs[pdef.typepath][pdef.name].append(pdef)
                if pdef.typepath != "/":
                    self._register_type_and_prefixes(pdef.typepath)

            for ddef in parsed.defines:
                self.defines_by_name[ddef.name].append(ddef)

    def _register_type_and_prefixes(self, tp: str) -> None:
        if not tp or tp == "/":
            return
        parts = [p for p in tp.split("/") if p]
        for k in range(1, len(parts) + 1):
            self.all_known_types.add("/" + "/".join(parts[:k]))

    def get_inheritance_chain(self, typepath: str) -> list[str]:
        """Return inheritance chain ordered child -> root (ending at /datum)."""
        norm = normalize_typepath(typepath)
        if norm == "/":
            return ["/"]

        chain: list[str] = []
        seen: set[str] = set()
        curr: str | None = norm

        while curr and curr not in seen:
            seen.add(curr)
            chain.append(curr)

            if curr == "/datum":
                break

            if curr in self.type_parent_overrides:
                curr = self.type_parent_overrides[curr]
            elif curr in _BUILTIN_ROOT_PARENTS:
                curr = _BUILTIN_ROOT_PARENTS[curr]
            else:
                parts = [p for p in curr.split("/") if p]
                if len(parts) > 1:
                    curr = "/" + "/".join(parts[:-1])
                else:
                    curr = "/datum"

        return chain

    def get_type(
        self,
        typepath: str,
        include_inherited: bool = True,
        filter_prefix: str | None = None,
    ) -> dict[str, Any]:
        """Return type definitions, resolved vars, and proc signatures."""
        self.ensure_fresh()
        norm = normalize_typepath(typepath)
        if norm != "/" and norm not in self.all_known_types:
            needle = norm.lower()
            suggestions = sorted(
                t for t in self.all_known_types if needle in t.lower() or t.lower().endswith(needle)
            )[:10]
            return {
                "error": f"DM typepath '{norm}' not found in repo '{self.repo_name}'.",
                "repo": self.repo_name,
                "suggestions": suggestions,
            }

        chain = self.get_inheritance_chain(norm) if include_inherited else [norm]
        prefix_lower = filter_prefix.strip().lower() if filter_prefix else None

        # Merge vars root -> child so child overrides parent
        merged_vars: dict[str, dict[str, Any]] = {}
        for tp in reversed(chain):
            for v in self.type_vars.get(tp, ()):
                existing = merged_vars.get(v.name)
                var_type = v.var_type or (existing["var_type"] if existing else None)
                merged_vars[v.name] = {
                    "name": v.name,
                    "value": v.value,
                    "var_type": var_type,
                    "defined_in": tp,
                    "file": v.file,
                    "line": v.line,
                    "inherited": tp != norm,
                }

        # Merge procs root -> child
        merged_procs: dict[str, dict[str, Any]] = {}
        for tp in reversed(chain):
            for pname, pdefs in self.type_procs.get(tp, {}).items():
                latest = pdefs[-1]
                merged_procs[pname] = {
                    "name": latest.name,
                    "kind": latest.kind,
                    "args": latest.args,
                    "signature": f"{latest.name}({latest.args}) -> {latest.file}:{latest.line}",
                    "defined_in": tp,
                    "file": latest.file,
                    "line": latest.line,
                    "end_line": latest.end_line,
                    "inherited": tp != norm,
                }

        vars_list = sorted(merged_vars.values(), key=lambda x: x["name"].lower())
        procs_list = sorted(merged_procs.values(), key=lambda x: x["name"].lower())
        if prefix_lower:
            vars_list = [v for v in vars_list if v["name"].lower().startswith(prefix_lower)]
            procs_list = [p for p in procs_list if p["name"].lower().startswith(prefix_lower)]

        defs = [
            {"file": f, "line": ln, "location": f"{f}:{ln}"}
            for f, ln in self.type_locations.get(norm, ())
        ]

        return {
            "repo": self.repo_name,
            "typepath": norm,
            "parent_type": chain[1] if len(chain) > 1 else None,
            "inheritance_chain": chain,
            "definitions": defs,
            "vars": vars_list,
            "procs": procs_list,
            "counts": {
                "definitions": len(defs),
                "vars": len(vars_list),
                "procs": len(procs_list),
            },
        }

    def get_proc(
        self,
        typepath: str,
        proc_name: str,
        include_parents: bool = False,
    ) -> dict[str, Any]:
        """Return exact source code of a proc/verb (and optionally parent ..() implementations)."""
        self.ensure_fresh()
        raw_tp = typepath.strip()
        if raw_tp in ("", "/", "/proc", "proc", "/verb", "verb"):
            norm_tp = "/"
        else:
            norm_tp = normalize_typepath(raw_tp)

        target_proc = proc_name.strip().lstrip("/")
        if "(" in target_proc:
            target_proc = target_proc.split("(", 1)[0].strip()

        if norm_tp != "/" and norm_tp not in self.all_known_types:
            return {
                "error": f"DM typepath '{norm_tp}' not found in repo '{self.repo_name}'.",
                "repo": self.repo_name,
            }

        chain = self.get_inheritance_chain(norm_tp)
        implementations: list[dict[str, Any]] = []

        for tp in chain:
            pdefs = self.type_procs.get(tp, {}).get(target_proc)
            if not pdefs:
                continue
            for pdef in pdefs:
                text = self._file_texts.get(pdef.file, "")
                lines = text.splitlines()
                start_idx = max(0, pdef.line - 1)
                end_idx = min(len(lines), pdef.end_line)
                source = "\n".join(lines[start_idx:end_idx])
                implementations.append(
                    {
                        "typepath": tp,
                        "name": pdef.name,
                        "kind": pdef.kind,
                        "args": pdef.args,
                        "file": pdef.file,
                        "line": pdef.line,
                        "end_line": pdef.end_line,
                        "inherited": tp != norm_tp,
                        "source": source,
                    }
                )
            if not include_parents:
                break

        if not implementations:
            avail = sorted(self.type_procs.get(norm_tp, {}).keys())[:15]
            return {
                "error": f"Proc '{target_proc}' not found on '{norm_tp}' or its parents.",
                "repo": self.repo_name,
                "typepath": norm_tp,
                "proc_name": target_proc,
                "available_procs_sample": avail,
            }

        return {
            "repo": self.repo_name,
            "typepath": norm_tp,
            "proc_name": target_proc,
            "include_parents": include_parents,
            "count": len(implementations),
            "implementations": implementations,
        }

    def search_defines(self, query: str, limit: int = 30) -> dict[str, Any]:
        """Search #define macros by name (exact, prefix, substring, or regex)."""
        self.ensure_fresh()
        clamped = max(1, min(limit, 200))
        q = query.strip()
        if not q:
            return {"error": "Query must not be empty.", "repo": self.repo_name}

        q_lower = q.lower()
        try:
            rx = re.compile(q, re.IGNORECASE)
        except re.error:
            rx = None

        scored: list[tuple[int, str, DefineDef]] = []
        for name, defs in self.defines_by_name.items():
            nl = name.lower()
            if name == q:
                rank = 0
            elif nl == q_lower:
                rank = 1
            elif nl.startswith(q_lower):
                rank = 2
            elif q_lower in nl:
                rank = 3
            elif rx is not None and rx.search(name):
                rank = 4
            else:
                continue

            for d in defs:
                scored.append((rank, name, d))

        scored.sort(key=lambda item: (item[0], item[1].lower(), item[2].file, item[2].line))
        total = len(scored)
        matches = [
            {
                "name": d.name,
                "params": d.params,
                "value": d.value,
                "file": d.file,
                "line": d.line,
                "end_line": d.end_line,
                "location": f"{d.file}:{d.line}",
            }
            for _, _, d in scored[:clamped]
        ]

        return {
            "repo": self.repo_name,
            "query": q,
            "total_matched": total,
            "truncated": total > clamped,
            "matches": matches,
        }

    def search_code(
        self,
        pattern: str,
        subpath: str | None = None,
        limit: int = 30,
    ) -> dict[str, Any]:
        """Regex search across .dm files with enclosing typepath/proc and 1-line context."""
        self.ensure_fresh()
        clamped = max(1, min(limit, 200))
        try:
            rx = re.compile(pattern)
        except re.error as exc:
            return {
                "error": f"Invalid regex pattern '{pattern}': {exc}",
                "repo": self.repo_name,
            }

        rel_files = sorted(self._file_texts.keys())
        norm_subpath: str | None = None
        if subpath and subpath.strip():
            norm_subpath = subpath.strip().replace("\\", "/").lstrip("/")
            filtered = [f for f in rel_files if f.lower().startswith(norm_subpath.lower())]
            if not filtered and not norm_subpath.lower().startswith("code/"):
                alt = f"code/{norm_subpath}".lower()
                filtered = [f for f in rel_files if f.lower().startswith(alt)]
            rel_files = filtered

        matches: list[dict[str, Any]] = []
        truncated = False

        for rel in rel_files:
            text = self._file_texts[rel]
            if not rx.search(text):
                continue

            lines = text.splitlines()
            # Build character offsets of line starts for O(log N) line lookup
            line_starts: list[int] = []
            offset = 0
            for raw_ln in text.splitlines(keepends=True):
                line_starts.append(offset)
                offset += len(raw_ln)

            parsed = self._file_parsed[rel]
            seen_lines: set[int] = set()

            for m in rx.finditer(text):
                line_idx = bisect.bisect_right(line_starts, m.start()) - 1
                if line_idx < 0:
                    line_idx = 0
                line_no = line_idx + 1
                if line_no in seen_lines:
                    continue
                seen_lines.add(line_no)

                enc_type, enc_proc = _find_enclosing_scope(parsed.scopes, line_no)
                ctx_start = max(0, line_idx - 1)
                ctx_end = min(len(lines), line_idx + 2)
                context = [
                    {"line": k + 1, "text": lines[k]}
                    for k in range(ctx_start, ctx_end)
                ]

                matches.append(
                    {
                        "file": rel,
                        "line": line_no,
                        "location": f"{rel}:{line_no}",
                        "enclosing_type": enc_type,
                        "enclosing_proc": enc_proc,
                        "match": lines[line_idx].strip(),
                        "context": context,
                    }
                )
                if len(matches) >= clamped:
                    truncated = True
                    break

            if truncated:
                break

        return {
            "repo": self.repo_name,
            "pattern": pattern,
            "subpath": norm_subpath,
            "returned": len(matches),
            "truncated": truncated,
            "matches": matches,
        }
