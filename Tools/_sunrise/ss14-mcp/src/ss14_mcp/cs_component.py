"""Fast C# component [DataField] extractor with mtime caching."""

from __future__ import annotations

import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from .config import to_posix_rel

_SKIP_DIRS = {"bin", "obj", ".git", ".vs", "node_modules", "__pycache__"}
_CONTENT_ROOTS = ("Content.Shared", "Content.Server", "Content.Client")

_CLASS_RE = re.compile(
    r"\b(?:public|internal|private|protected)?\s*"
    r"(?:sealed\s+|abstract\s+|static\s+|partial\s+)*"
    r"class\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*Component)\b"
    r"(?:\s*:\s*(?P<bases>[^{]+))?\s*\{",
    re.MULTILINE,
)

_DATAFIELD_RE = re.compile(r"^DataField(?:\s*\((?P<args>.*)\))?$", re.DOTALL)
_VIEWVARS_RE = re.compile(r"^ViewVariables(?:\s*\((?P<args>.*)\))?$", re.DOTALL)
_AUTO_STATE_RE = re.compile(
    r"^AutoGenerateComponentState(?:\s*\((?P<args>.*)\))?$", re.DOTALL
)
_AUTO_PAUSE_RE = re.compile(
    r"^AutoGenerateComponentPause(?:\s*\((?P<args>.*)\))?$", re.DOTALL
)
_CUSTOM_SER_RE = re.compile(
    r"customTypeSerializer\s*:\s*typeof\s*\(\s*([^)]+)\s*\)"
)
_STRING_ARG_RE = re.compile(r'^\s*"([^"]+)"')
_TAG_ARG_RE = re.compile(r'tag\s*:\s*"([^"]+)"')
_REQUIRED_RE = re.compile(r"\brequired\s*:\s*true\b")

_FIELD_DECL_RE = re.compile(
    r"^(?:(?:public|private|protected|internal)\s+)?"
    r"(?:(?:readonly|const|static|new|override|virtual|required|volatile|unsafe)\s+)*"
    r"(?P<type>[A-Za-z_][A-Za-z0-9_<>\[\],\s\.\?]*?)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*"
    r"(?P<tail>(?:\{.*\}\s*(?:=\s*.*)?|=>.*|(?:\s*=\s*.*)?)\s*;?)$",
    re.DOTALL,
)


def _strip_comments_and_directives(source: str) -> str:
    """Replace C# //, ///, /* */ comments and #preprocessor lines with spaces, preserving newlines."""
    out: list[str] = []
    i = 0
    n = len(source)
    in_str = False
    in_char = False
    verbatim = False
    at_line_start = True

    while i < n:
        ch = source[i]
        nxt = source[i + 1] if i + 1 < n else ""

        if in_str:
            out.append(ch)
            if ch == "\n":
                at_line_start = False
            if verbatim:
                if ch == '"' and nxt == '"':
                    out.append(nxt)
                    i += 2
                    continue
                if ch == '"':
                    in_str = False
                    verbatim = False
            else:
                if ch == "\\" and i + 1 < n:
                    out.append(nxt)
                    i += 2
                    continue
                if ch == '"':
                    in_str = False
            i += 1
            continue

        if in_char:
            out.append(ch)
            if ch == "\\" and i + 1 < n:
                out.append(nxt)
                i += 2
                continue
            if ch == "'":
                in_char = False
            i += 1
            continue

        # Preprocessor directive (#region, #endregion, #if, #endif, #pragma, etc.)
        if at_line_start and ch == "#":
            while i < n and source[i] != "\n":
                out.append(" ")
                i += 1
            continue

        if ch == "@" and nxt == '"':
            in_str = True
            verbatim = True
            at_line_start = False
            out.append('@"')
            i += 2
            continue
        if ch == '"':
            in_str = True
            at_line_start = False
            out.append(ch)
            i += 1
            continue
        if ch == "'":
            in_char = True
            at_line_start = False
            out.append(ch)
            i += 1
            continue

        if ch == "/" and nxt == "/":
            out.append("  ")
            i += 2
            while i < n and source[i] != "\n":
                out.append(" ")
                i += 1
            continue

        if ch == "/" and nxt == "*":
            out.append("  ")
            i += 2
            while i < n:
                if source[i] == "*" and i + 1 < n and source[i + 1] == "/":
                    out.append("  ")
                    i += 2
                    break
                if source[i] == "\n":
                    out.append("\n")
                    at_line_start = True
                else:
                    out.append(" ")
                i += 1
            continue

        out.append(ch)
        if ch == "\n":
            at_line_start = True
        elif not ch.isspace():
            at_line_start = False
        i += 1

    return "".join(out)


def _to_camel_case(name: str) -> str:
    if not name:
        return name
    return name[0].lower() + name[1:]


def _split_attributes(attr_text: str) -> list[str]:
    """Split attribute block text `[A, B(1, 2)]` into individual attribute strings."""
    attrs: list[str] = []
    depth_paren = 0
    in_str = False
    buf: list[str] = []

    i = 0
    while i < len(attr_text):
        ch = attr_text[i]
        if ch == '"' and (i == 0 or attr_text[i - 1] != "\\"):
            in_str = not in_str
        elif not in_str:
            if ch == "(":
                depth_paren += 1
            elif ch == ")":
                depth_paren = max(0, depth_paren - 1)
            elif ch == "," and depth_paren == 0:
                item = "".join(buf).strip()
                if item:
                    attrs.append(item)
                buf.clear()
                i += 1
                continue
        buf.append(ch)
        i += 1

    tail = "".join(buf).strip()
    if tail:
        attrs.append(tail)
    return attrs


def _find_matching_brace(text: str, open_idx: int) -> int:
    depth = 0
    in_str = False
    in_verbatim = False
    in_char = False
    i = open_idx
    n = len(text)
    while i < n:
        ch = text[i]
        if in_verbatim:
            if ch == '"':
                if i + 1 < n and text[i + 1] == '"':
                    i += 2
                    continue
                in_verbatim = False
        elif in_str:
            if ch == "\\" and i + 1 < n:
                i += 2
                continue
            if ch == '"':
                in_str = False
        elif in_char:
            if ch == "\\" and i + 1 < n:
                i += 2
                continue
            if ch == "'":
                in_char = False
        else:
            if ch == "@" and i + 1 < n and text[i + 1] == '"':
                in_verbatim = True
                i += 2
                continue
            if ch == "$" and i + 2 < n and text[i + 1] == "@" and text[i + 2] == '"':
                in_verbatim = True
                i += 3
                continue
            if ch == '"':
                in_str = True
            elif ch == "'":
                in_char = True
            elif ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
                if depth == 0:
                    return i
        i += 1
    return n - 1


def _extract_preceding_attrs(text: str, pos: int) -> list[str]:
    """Extract contiguous [Attr(...)] blocks immediately preceding pos."""
    blocks: list[str] = []
    idx = pos - 1
    while idx >= 0:
        while idx >= 0 and text[idx].isspace():
            idx -= 1
        if idx < 0 or text[idx] != "]":
            break
        depth = 0
        in_str = False
        end = idx
        while idx >= 0:
            ch = text[idx]
            if ch == '"' and (idx == 0 or text[idx - 1] != "\\"):
                in_str = not in_str
            elif not in_str:
                if ch == "]":
                    depth += 1
                elif ch == "[":
                    depth -= 1
                    if depth == 0:
                        blocks.append(text[idx + 1 : end])
                        idx -= 1
                        break
            idx -= 1
        else:
            break

    attrs: list[str] = []
    for block in reversed(blocks):
        attrs.extend(_split_attributes(block))
    return attrs


def _extract_default_value(tail: str) -> str | None:
    """Extract initializer value after top-level '=' in a field/property tail."""
    stripped = tail.strip()
    if not stripped or stripped == ";":
        return None

    # Skip property accessor block `{ get; set; }` if present before `=`
    idx = 0
    n = len(stripped)
    if stripped[0] == "{":
        close_idx = _find_matching_brace(stripped, 0)
        idx = close_idx + 1

    # Find top-level '=' (not '=>')
    depth_paren = 0
    depth_brace = 0
    in_str = False
    while idx < n:
        ch = stripped[idx]
        if ch == '"' and (idx == 0 or stripped[idx - 1] != "\\"):
            in_str = not in_str
        elif not in_str:
            if ch == "(":
                depth_paren += 1
            elif ch == ")":
                depth_paren = max(0, depth_paren - 1)
            elif ch == "{":
                depth_brace += 1
            elif ch == "}":
                depth_brace = max(0, depth_brace - 1)
            elif (
                ch == "="
                and depth_paren == 0
                and depth_brace == 0
                and (idx + 1 >= n or stripped[idx + 1] != ">")
            ):
                val = stripped[idx + 1 :].rstrip(";").strip()
                if not val:
                    return None
                if "\n" not in val and "\r" not in val:
                    return val
                return " ".join(line.strip() for line in val.splitlines() if line.strip())
        idx += 1
    return None


def _parse_class_members(
    body: str,
    body_start_line: int,
    rel_path: str,
) -> list[dict[str, Any]]:
    """Parse top-level [DataField] fields and properties inside a component class body."""
    fields: list[dict[str, Any]] = []
    n = len(body)
    i = 0
    in_str = False
    in_verbatim = False
    in_char = False
    pending_attrs: list[str] = []
    stmt_start: int | None = None
    seen_equals = False

    while i < n:
        ch = body[i]
        if in_verbatim:
            if ch == '"':
                if i + 1 < n and body[i + 1] == '"':
                    i += 2
                    continue
                in_verbatim = False
            i += 1
            continue
        if in_str:
            if ch == "\\" and i + 1 < n:
                i += 2
                continue
            if ch == '"':
                in_str = False
            i += 1
            continue
        if in_char:
            if ch == "\\" and i + 1 < n:
                i += 2
                continue
            if ch == "'":
                in_char = False
            i += 1
            continue

        if ch == "@" and i + 1 < n and body[i + 1] == '"':
            in_verbatim = True
            i += 2
            continue
        if ch == "$" and i + 2 < n and body[i + 1] == "@" and body[i + 2] == '"':
            in_verbatim = True
            i += 3
            continue
        if ch == '"':
            in_str = True
            i += 1
            continue
        if ch == "'":
            in_char = True
            i += 1
            continue

        if ch.isspace():
            i += 1
            continue

        if ch == "[" and stmt_start is None:
            attr_depth = 0
            start_attr = i + 1
            while i < n:
                c2 = body[i]
                if c2 == '"' and (i == 0 or body[i - 1] != "\\"):
                    in_str = not in_str
                elif not in_str:
                    if c2 == "[":
                        attr_depth += 1
                    elif c2 == "]":
                        attr_depth -= 1
                        if attr_depth == 0:
                            pending_attrs.extend(
                                _split_attributes(body[start_attr:i])
                            )
                            i += 1
                            break
                i += 1
            continue

        if stmt_start is None:
            stmt_start = i
            seen_equals = False

        if ch == "=" and (i + 1 >= n or body[i + 1] != ">"):
            seen_equals = True
            i += 1
            continue

        if ch == "{":
            close_idx = _find_matching_brace(body, i)
            if seen_equals:
                # Object/collection initializer like `= new() { DamageDict = { ["Blunt"] = 2f } };`
                # Continue scanning until the terminating `;`.
                i = close_idx + 1
                continue

            # Property accessor `{ get; set; }` (may be followed by `= default;`) or method/nested type.
            j = close_idx + 1
            while j < n and body[j].isspace():
                j += 1
            if j < n and body[j] == "=":
                seen_equals = True
                i = j + 1
                continue

            end_pos = close_idx + 1
            stmt = body[stmt_start:end_pos].strip()
            _maybe_record_field(
                stmt,
                pending_attrs,
                body_start_line + body[:stmt_start].count("\n"),
                rel_path,
                fields,
            )
            pending_attrs.clear()
            stmt_start = None
            seen_equals = False
            i = end_pos
            continue

        if ch == ";":
            stmt = body[stmt_start : i + 1].strip()
            _maybe_record_field(
                stmt,
                pending_attrs,
                body_start_line + body[:stmt_start].count("\n"),
                rel_path,
                fields,
            )
            pending_attrs.clear()
            stmt_start = None
            seen_equals = False
            i += 1
            continue

        i += 1

    return fields


def _maybe_record_field(
    stmt: str,
    attrs: list[str],
    line_no: int,
    rel_path: str,
    out: list[dict[str, Any]],
) -> None:
    df_attr = next((a for a in attrs if _DATAFIELD_RE.match(a.strip())), None)
    if df_attr is None:
        return

    # Skip methods/constructors even if somehow preceded by DataField
    m_decl = _FIELD_DECL_RE.match(stmt)
    if not m_decl:
        return

    cs_type = " ".join(m_decl.group("type").split())
    cs_name = m_decl.group("name")
    tail = m_decl.group("tail")
    default_val = _extract_default_value(tail)

    df_match = _DATAFIELD_RE.match(df_attr.strip())
    df_args = (df_match.group("args") or "") if df_match else ""

    yaml_name = _to_camel_case(cs_name)
    m_tag = _TAG_ARG_RE.search(df_args)
    if m_tag:
        yaml_name = m_tag.group(1)
    else:
        m_pos = _STRING_ARG_RE.search(df_args)
        if m_pos:
            yaml_name = m_pos.group(1)

    required = bool(_REQUIRED_RE.search(df_args))
    m_ser = _CUSTOM_SER_RE.search(df_args)
    custom_serializer = m_ser.group(1).strip() if m_ser else None

    auto_networked = any(a.strip().startswith("AutoNetworkedField") for a in attrs)
    auto_paused = any(a.strip().startswith("AutoPausedField") for a in attrs)
    vv_attr = next((a.strip() for a in attrs if _VIEWVARS_RE.match(a.strip())), None)

    out.append(
        {
            "yaml_name": yaml_name,
            "cs_name": cs_name,
            "cs_type": cs_type,
            "default": default_val,
            "required": required,
            "auto_networked": auto_networked,
            "auto_paused": auto_paused,
            "view_variables": vv_attr,
            "custom_serializer": custom_serializer,
            "file": rel_path,
            "line": line_no,
        }
    )


@dataclass(slots=True)
class _ParsedClassFragment:
    name: str
    file: str
    line: int
    bases: list[str]
    auto_generate_state: bool
    auto_generate_state_args: str | None
    auto_generate_pause: bool
    auto_generate_pause_args: str | None
    networked: bool
    register_component: bool
    access: list[str]
    fields: list[dict[str, Any]] = field(default_factory=list)


def _parse_cs_file(path: Path, rel_path: str) -> list[_ParsedClassFragment]:
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    cleaned = _strip_comments_and_directives(text)
    fragments: list[_ParsedClassFragment] = []

    for match in _CLASS_RE.finditer(cleaned):
        class_name = match.group("name")
        bases_raw = match.group("bases") or ""
        bases = [b.strip() for b in bases_raw.split(",") if b.strip()]
        attrs = _extract_preceding_attrs(cleaned, match.start())

        open_brace = match.end() - 1
        close_brace = _find_matching_brace(cleaned, open_brace)
        body = cleaned[open_brace + 1 : close_brace]
        class_line = cleaned[: match.start()].count("\n") + 1
        body_start_line = cleaned[: open_brace + 1].count("\n") + 1

        auto_state = False
        auto_state_args: str | None = None
        auto_pause = False
        auto_pause_args: str | None = None
        for attr in attrs:
            s = attr.strip()
            m_st = _AUTO_STATE_RE.match(s)
            if m_st:
                auto_state = True
                raw_args = (m_st.group("args") or "").strip()
                if raw_args:
                    auto_state_args = " ".join(raw_args.split())
            m_pa = _AUTO_PAUSE_RE.match(s)
            if m_pa:
                auto_pause = True
                raw_args = (m_pa.group("args") or "").strip()
                if raw_args:
                    auto_pause_args = " ".join(raw_args.split())

        access_list = [a.strip() for a in attrs if a.strip().startswith("Access(")]
        fragments.append(
            _ParsedClassFragment(
                name=class_name,
                file=rel_path,
                line=class_line,
                bases=bases,
                auto_generate_state=auto_state,
                auto_generate_state_args=auto_state_args,
                auto_generate_pause=auto_pause,
                auto_generate_pause_args=auto_pause_args,
                networked=any(a.strip().startswith("NetworkedComponent") for a in attrs),
                register_component=any(
                    a.strip().startswith("RegisterComponent") for a in attrs
                ),
                access=access_list,
                fields=_parse_class_members(body, body_start_line, rel_path),
            )
        )

    return fragments


def _scan_component_cs(root_dir: Path) -> dict[Path, tuple[int, int]]:
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
                        if entry.name not in _SKIP_DIRS:
                            stack.append(Path(entry.path))
                    elif entry.is_file(follow_symlinks=False):
                        name = entry.name
                        if name.endswith(".cs") and "Component" in name:
                            st = entry.stat(follow_symlinks=False)
                            found[Path(entry.path)] = (st.st_mtime_ns, st.st_size)
        except OSError:
            continue
    return found


class ComponentIndex:
    """Incremental mtime-cached index of SS14 C# components."""

    def __init__(self, repo_root: Path) -> None:
        self.repo_root = repo_root
        self._file_stats: dict[str, tuple[int, int]] = {}
        self._file_fragments: dict[str, list[_ParsedClassFragment]] = {}
        self._by_name: dict[str, list[_ParsedClassFragment]] = {}
        self._initialized = False

    def ensure_fresh(self) -> bool:
        current_files: dict[Path, tuple[int, int]] = {}
        for folder in _CONTENT_ROOTS:
            current_files.update(_scan_component_cs(self.repo_root / folder))

        current_by_rel: dict[str, tuple[Path, tuple[int, int]]] = {}
        for path, stat_sig in current_files.items():
            current_by_rel[to_posix_rel(path, self.repo_root)] = (path, stat_sig)

        removed = set(self._file_stats.keys()) - set(current_by_rel.keys())
        changed = [
            (rel, path, sig)
            for rel, (path, sig) in current_by_rel.items()
            if self._file_stats.get(rel) != sig
        ]

        if self._initialized and not removed and not changed:
            return False

        for rel in removed:
            self._file_stats.pop(rel, None)
            self._file_fragments.pop(rel, None)

        for rel, path, sig in changed:
            try:
                self._file_fragments[rel] = _parse_cs_file(path, rel)
                self._file_stats[rel] = sig
            except OSError:
                self._file_fragments[rel] = []

        by_name: dict[str, list[_ParsedClassFragment]] = {}
        for rel in sorted(self._file_fragments.keys()):
            for frag in self._file_fragments[rel]:
                by_name.setdefault(frag.name.lower(), []).append(frag)

        self._by_name = by_name
        self._initialized = True
        return True

    def inspect_component(self, component_name: str) -> dict[str, Any]:
        """Return [DataField] and networking metadata for a C# component."""
        self.ensure_fresh()
        raw = component_name.strip()
        if not raw:
            return {"error": "component_name must not be empty."}

        candidates = [raw]
        if not raw.endswith("Component"):
            candidates.append(f"{raw}Component")

        fragments: list[_ParsedClassFragment] | None = None
        for cand in candidates:
            fragments = self._by_name.get(cand.lower())
            if fragments:
                break

        # Lazy fallback into RobustToolbox for engine components (e.g. TransformComponent)
        if not fragments:
            rt_dir = self.repo_root / "RobustToolbox"
            if rt_dir.is_dir():
                for cand in candidates:
                    for path in _scan_component_cs(rt_dir):
                        if path.stem.lower() == cand.lower():
                            rel = to_posix_rel(path, self.repo_root)
                            parsed = _parse_cs_file(path, rel)
                            matched = [
                                f for f in parsed if f.name.lower() == cand.lower()
                            ]
                            if matched:
                                fragments = matched
                                break
                    if fragments:
                        break

        if not fragments:
            needle = raw.lower()
            suggestions = [
                frags[0].name
                for key, frags in self._by_name.items()
                if needle in key
            ][:10]
            return {
                "error": f"Component '{component_name}' not found in Content.* or RobustToolbox.",
                "component_name": component_name,
                "suggestions": suggestions,
            }

        canonical_name = fragments[0].name
        yaml_type = (
            canonical_name[: -len("Component")]
            if canonical_name.endswith("Component")
            else canonical_name
        )
        all_fields: list[dict[str, Any]] = []
        bases: list[str] = []
        access: list[str] = []
        files: list[str] = []
        state_args: str | None = None
        pause_args: str | None = None

        for frag in fragments:
            files.append(f"{frag.file}:{frag.line}")
            for b in frag.bases:
                if b not in bases:
                    bases.append(b)
            for a in frag.access:
                if a not in access:
                    access.append(a)
            if frag.auto_generate_state_args and state_args is None:
                state_args = frag.auto_generate_state_args
            if frag.auto_generate_pause_args and pause_args is None:
                pause_args = frag.auto_generate_pause_args
            all_fields.extend(frag.fields)

        result: dict[str, Any] = {
            "component": canonical_name,
            "yaml_type": yaml_type,
            "files": files,
            "bases": bases,
            "register_component": any(f.register_component for f in fragments),
            "networked": any(f.networked for f in fragments),
            "auto_generate_component_state": any(
                f.auto_generate_state for f in fragments
            ),
            "auto_generate_component_pause": any(
                f.auto_generate_pause for f in fragments
            ),
            "access": access,
            "data_fields_count": len(all_fields),
            "data_fields": all_fields,
        }
        if state_args is not None:
            result["auto_generate_component_state_args"] = state_args
        if pause_args is not None:
            result["auto_generate_component_pause_args"] = pause_args
        return result
