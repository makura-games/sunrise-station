"""DreamMaker (.dm) parser: indentation blocks, relative paths, comments, #define, and scopes."""

from __future__ import annotations

import re
from dataclasses import dataclass, field


@dataclass(slots=True)
class VarDef:
    """Variable declaration or override on a DM type."""

    name: str
    value: str | None
    is_decl: bool
    var_type: str | None
    file: str
    line: int


@dataclass(slots=True)
class ProcDef:
    """Procedure or verb definition/override on a DM type."""

    typepath: str
    name: str
    kind: str  # "proc", "verb", or "override"
    is_new: bool
    args: str
    file: str
    line: int
    end_line: int


@dataclass(slots=True)
class DefineDef:
    """Preprocessor #define macro."""

    name: str
    params: str | None
    value: str
    file: str
    line: int
    end_line: int


@dataclass(slots=True)
class ScopeSpan:
    """Line-range span for enclosing typepath/proc lookup."""

    start_line: int
    end_line: int
    typepath: str | None
    proc_name: str | None


@dataclass(slots=True)
class ParsedDmFile:
    """Parsed structure of a single .dm file."""

    file: str
    type_locations: dict[str, list[int]] = field(default_factory=dict)
    vars_by_type: dict[str, list[VarDef]] = field(default_factory=dict)
    parent_types: dict[str, str] = field(default_factory=dict)
    procs: list[ProcDef] = field(default_factory=list)
    defines: list[DefineDef] = field(default_factory=list)
    scopes: list[ScopeSpan] = field(default_factory=list)


@dataclass(slots=True)
class _StackEntry:
    indent: int
    typepath: str
    mode: str  # "type", "var", "proc", "verb"
    var_prefix: str
    start_line: int


_VAR_MODIFIERS = frozenset(
    {
        "global",
        "static",
        "tmp",
        "const",
        "final",
        "private",
        "protected",
        "public",
        "SpacemanDMM_final",
        "SpacemanDMM_private",
        "SpacemanDMM_protected",
    }
)
_VAR_MACRO_PREFIXES = ("VAR_PRIVATE", "VAR_PROTECTED", "VAR_FINAL", "VAR_PUBLIC")
_DM_BUILTIN_ROOTS = frozenset(
    {
        "datum",
        "atom",
        "area",
        "turf",
        "obj",
        "mob",
        "client",
        "world",
        "list",
        "image",
        "icon",
        "sound",
        "matrix",
        "regex",
        "savefile",
    }
)
_DM_KEYWORDS = frozenset(
    {
        "if",
        "else",
        "for",
        "while",
        "do",
        "switch",
        "case",
        "default",
        "return",
        "break",
        "continue",
        "goto",
        "spawn",
        "try",
        "catch",
        "throw",
        "del",
        "new",
        "in",
        "as",
        "to",
        "step",
        "set",
        "var",
        "proc",
        "verb",
        "tmp",
        "const",
        "global",
        "static",
        "final",
        "private",
        "protected",
        "public",
    }
)

_RE_DEFINE = re.compile(r"^#\s*define\s+([A-Za-z_][A-Za-z0-9_]*)(.*)$")
_RE_TYPEPATH = re.compile(r"^/[A-Za-z_][A-Za-z0-9_/]*$")
_RE_REL_TYPE = re.compile(r"^[A-Za-z_][A-Za-z0-9_/]*$")
_RE_IDENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
_RE_VAR_NAME = re.compile(r"^([A-Za-z_][A-Za-z0-9_]*)")
_RE_PROC_PREFIX = re.compile(r"^([A-Za-z_/][A-Za-z0-9_/]*)\s*\(")


def normalize_typepath(path: str) -> str:
    """Normalize a DM typepath to leading '/' with no trailing '/' (except root '/')."""
    p = path.strip()
    if not p:
        return "/"
    if not p.startswith("/"):
        p = "/" + p
    while "//" in p:
        p = p.replace("//", "/")
    if len(p) > 1:
        p = p.rstrip("/")
    return p


def _calc_indent(line: str) -> int:
    """Compute indentation column count (tab = 4, space = 1)."""
    indent = 0
    for ch in line:
        if ch == "\t":
            indent += 4
        elif ch == " ":
            indent += 1
        else:
            break
    return indent


def _strip_comments_and_ml_strings(lines: list[str]) -> list[str]:
    """Strip //, /* */ (nested), and multiline {\" ... \"} strings, preserving line count."""
    out: list[str] = []
    block_depth = 0
    in_ml_str = False

    for line in lines:
        if block_depth == 0 and not in_ml_str:
            if "/*" not in line and "//" not in line and '{"' not in line:
                out.append(line)
                continue

        buf: list[str] = []
        i = 0
        n = len(line)
        in_dq = False
        in_sq = False

        while i < n:
            ch = line[i]
            nxt = line[i + 1] if i + 1 < n else ""

            if in_ml_str:
                if ch == '"' and nxt == "}":
                    in_ml_str = False
                    i += 2
                    continue
                i += 1
                continue

            if block_depth > 0:
                if ch == "/" and nxt == "*":
                    block_depth += 1
                    i += 2
                    continue
                if ch == "*" and nxt == "/":
                    block_depth -= 1
                    i += 2
                    continue
                i += 1
                continue

            if in_dq:
                buf.append(ch)
                if ch == "\\" and i + 1 < n:
                    buf.append(nxt)
                    i += 2
                    continue
                if ch == '"':
                    in_dq = False
                i += 1
                continue

            if in_sq:
                buf.append(ch)
                if ch == "\\" and i + 1 < n:
                    buf.append(nxt)
                    i += 2
                    continue
                if ch == "'":
                    in_sq = False
                i += 1
                continue

            # Outside strings and comments
            if ch == "/" and nxt == "/":
                break
            if ch == "/" and nxt == "*":
                block_depth += 1
                i += 2
                continue
            if ch == "{" and nxt == '"':
                in_ml_str = True
                buf.append('""')
                i += 2
                continue
            if ch == '"':
                in_dq = True
                buf.append(ch)
                i += 1
                continue
            if ch == "'":
                in_sq = True
                buf.append(ch)
                i += 1
                continue

            buf.append(ch)
            i += 1

        out.append("".join(buf))

    return out


def _find_top_level_char(text: str, target: str) -> int:
    """Find first occurrence of target outside (), [], strings, and icon literals."""
    paren = 0
    bracket = 0
    in_dq = False
    in_sq = False
    i = 0
    n = len(text)
    while i < n:
        ch = text[i]
        if in_dq:
            if ch == "\\":
                i += 2
                continue
            if ch == '"':
                in_dq = False
        elif in_sq:
            if ch == "\\":
                i += 2
                continue
            if ch == "'":
                in_sq = False
        else:
            if ch == '"':
                in_dq = True
            elif ch == "'":
                in_sq = True
            elif ch == "(":
                paren += 1
            elif ch == ")" and paren > 0:
                paren -= 1
            elif ch == "[":
                bracket += 1
            elif ch == "]" and bracket > 0:
                bracket -= 1
            elif ch == target and paren == 0 and bracket == 0:
                return i
        i += 1
    return -1


def _paren_delta(text: str) -> int:
    """Net parenthesis depth change outside strings and icon literals."""
    delta = 0
    in_dq = False
    in_sq = False
    i = 0
    n = len(text)
    while i < n:
        ch = text[i]
        if in_dq:
            if ch == "\\":
                i += 2
                continue
            if ch == '"':
                in_dq = False
        elif in_sq:
            if ch == "\\":
                i += 2
                continue
            if ch == "'":
                in_sq = False
        else:
            if ch == '"':
                in_dq = True
            elif ch == "'":
                in_sq = True
            elif ch == "(":
                delta += 1
            elif ch == ")":
                delta -= 1
        i += 1
    return delta


def _split_proc_args(text: str, open_idx: int) -> tuple[str, str, int]:
    """Starting at '(' index, extract (args, rest_after_close, remaining_paren_depth)."""
    depth = 0
    in_dq = False
    in_sq = False
    i = open_idx
    n = len(text)
    while i < n:
        ch = text[i]
        if in_dq:
            if ch == "\\":
                i += 2
                continue
            if ch == '"':
                in_dq = False
        elif in_sq:
            if ch == "\\":
                i += 2
                continue
            if ch == "'":
                in_sq = False
        else:
            if ch == '"':
                in_dq = True
            elif ch == "'":
                in_sq = True
            elif ch == "(":
                depth += 1
            elif ch == ")":
                depth -= 1
                if depth == 0:
                    return text[open_idx + 1 : i].strip(), text[i + 1 :].strip(), 0
        i += 1
    return text[open_idx + 1 :].strip(), "", depth


def _parse_var_decl_lhs(lhs: str, prefix: str = "") -> tuple[str, str | None] | None:
    """Parse a DM var declaration LHS into (var_name, var_type)."""
    raw = lhs.strip()
    if not raw:
        return None

    # Strip leading VAR_PRIVATE/... macro if present
    for macro in _VAR_MACRO_PREFIXES:
        if raw == macro:
            return None
        if raw.startswith(macro + "/"):
            raw = raw[len(macro) + 1 :]
            break

    if prefix and not raw.startswith("var/") and not raw.startswith("/"):
        combined = f"{prefix}/{raw}"
    else:
        combined = raw

    parts = [p for p in combined.strip("/").split("/") if p]
    if not parts:
        return None
    if parts[0] == "var":
        parts = parts[1:]
    if not parts:
        return None

    raw_name = parts[-1]
    m = _RE_VAR_NAME.match(raw_name)
    if not m:
        return None
    var_name = m.group(1)

    type_parts = [p for p in parts[:-1] if p not in _VAR_MODIFIERS]
    var_type = "/" + "/".join(type_parts) if type_parts else None
    return var_name, var_type


def _resolve_proc_owner_and_name(
    full_path: str,
    stack_type: str | None,
    stack_mode: str | None,
) -> tuple[str, str, str, bool] | None:
    """Resolve (owner_typepath, proc_name, kind, is_new) from a proc header path."""
    raw = full_path.strip()
    if not raw:
        return None

    if raw.startswith("/"):
        parts = [p for p in raw.split("/") if p]
        if not parts:
            return None
        proc_name = parts[-1]
        prefix = parts[:-1]
        if prefix and prefix[-1] in ("proc", "verb"):
            kind = prefix[-1]
            owner_parts = prefix[:-1]
            owner = "/" + "/".join(owner_parts) if owner_parts else "/"
            return owner, proc_name, kind, True
        owner = "/" + "/".join(prefix) if prefix else "/"
        return owner, proc_name, "override", False

    # Relative proc path
    parts = [p for p in raw.split("/") if p]
    if not parts:
        return None
    base = stack_type or "/"
    proc_name = parts[-1]
    prefix = parts[:-1]
    if prefix and prefix[-1] in ("proc", "verb"):
        kind = prefix[-1]
        sub_parts = prefix[:-1]
        if base == "/":
            owner = "/" + "/".join(sub_parts) if sub_parts else "/"
        else:
            owner = f"{base}/{'/'.join(sub_parts)}" if sub_parts else base
        return normalize_typepath(owner), proc_name, kind, True

    if stack_mode in ("proc", "verb") and not prefix:
        return normalize_typepath(base), proc_name, stack_mode, True

    if base == "/":
        owner = "/" + "/".join(prefix) if prefix else "/"
    else:
        owner = f"{base}/{'/'.join(prefix)}" if prefix else base
    return normalize_typepath(owner), proc_name, "override", False


def _parse_define_line(full_line: str, rel_path: str, start_line: int, end_line: int) -> DefineDef | None:
    m = _RE_DEFINE.match(full_line.strip())
    if not m:
        return None
    name = m.group(1)
    rest = m.group(2)
    params: str | None = None
    value = ""

    # Function-like macro only if '(' is immediately adjacent to macro name
    if rest.startswith("("):
        close_idx = rest.find(")")
        if close_idx != -1:
            params = rest[1:close_idx].strip()
            value = rest[close_idx + 1 :].strip()
        else:
            value = rest.strip()
    else:
        value = rest.strip()

    # Collapse continuation whitespace cleanly
    if "\n" in value or "\t" in value:
        value = " ".join(value.split())

    return DefineDef(
        name=name,
        params=params,
        value=value,
        file=rel_path,
        line=start_line,
        end_line=end_line,
    )


def parse_dm_file(rel_path: str, text: str) -> ParsedDmFile:
    """Parse a DreamMaker .dm file into types, vars, procs/verbs, #defines, and scopes."""
    result = ParsedDmFile(file=rel_path)
    raw_lines = text.splitlines()
    if not raw_lines:
        return result

    clean_lines = _strip_comments_and_ml_strings(raw_lines)
    n_lines = len(clean_lines)

    # Precompute stripped lines, indents, and next non-empty line indent
    stripped_lines: list[str] = [""] * n_lines
    indents: list[int] = [0] * n_lines
    for idx, ln in enumerate(clean_lines):
        s = ln.strip()
        stripped_lines[idx] = s
        if s:
            indents[idx] = _calc_indent(ln)

    next_nonempty_indent: list[int] = [-1] * n_lines
    last_indent = -1
    for idx in range(n_lines - 1, -1, -1):
        next_nonempty_indent[idx] = last_indent
        if stripped_lines[idx]:
            last_indent = indents[idx]

    stack: list[_StackEntry] = []
    active_proc: ProcDef | None = None
    active_proc_indent = -1
    active_proc_last_line = 0
    proc_ifdef_depth = 0
    collecting_proc_args = False
    proc_args_depth = 0
    proc_args_parts: list[str] = []

    active_var: VarDef | None = None
    active_var_paren_depth = 0
    active_var_cont = False

    prev_nonempty_line = 0

    def _record_type(tp: str, line_no: int) -> None:
        locs = result.type_locations.get(tp)
        if locs is None:
            result.type_locations[tp] = [line_no]
        elif not locs or locs[-1] != line_no:
            locs.append(line_no)

    def _close_active_proc() -> None:
        nonlocal active_proc, active_proc_indent, active_proc_last_line, proc_ifdef_depth, collecting_proc_args
        if active_proc is None:
            return
        active_proc.end_line = max(active_proc.line, active_proc_last_line)
        result.scopes.append(
            ScopeSpan(
                start_line=active_proc.line,
                end_line=active_proc.end_line,
                typepath=active_proc.typepath,
                proc_name=active_proc.name,
            )
        )
        active_proc = None
        active_proc_indent = -1
        active_proc_last_line = 0
        proc_ifdef_depth = 0
        collecting_proc_args = False

    def _pop_stack_to(target_indent: int, end_line_no: int) -> None:
        while stack and stack[-1].indent >= target_indent:
            popped = stack.pop()
            if popped.mode == "type" and popped.typepath != "/":
                result.scopes.append(
                    ScopeSpan(
                        start_line=popped.start_line,
                        end_line=max(popped.start_line, end_line_no),
                        typepath=popped.typepath,
                        proc_name=None,
                    )
                )

    i = 0
    while i < n_lines:
        line_no = i + 1
        s = stripped_lines[i]
        if not s:
            i += 1
            continue

        indent = indents[i]

        # 1. Preprocessor directives (#define, #ifdef, etc.)
        if s.startswith("#"):
            start_ln = line_no
            parts = [s]
            j = i
            while parts[-1].endswith("\\") and j + 1 < n_lines:
                parts[-1] = parts[-1][:-1].rstrip()
                j += 1
                parts.append(stripped_lines[j])
            end_ln = j + 1
            joined = " ".join(p for p in parts if p)
            directive = joined.lstrip("#").lstrip().split(None, 1)[0] if joined.lstrip("#").lstrip() else ""
            if directive == "define":
                d = _parse_define_line(joined, rel_path, start_ln, end_ln)
                if d is not None:
                    result.defines.append(d)
            if active_proc is not None:
                if directive in ("if", "ifdef", "ifndef"):
                    proc_ifdef_depth += 1
                    active_proc_last_line = end_ln
                elif directive in ("elif", "else") and proc_ifdef_depth > 0:
                    active_proc_last_line = end_ln
                elif directive == "endif" and proc_ifdef_depth > 0:
                    proc_ifdef_depth -= 1
                    active_proc_last_line = end_ln
            prev_nonempty_line = end_ln
            i = j + 1
            continue

        # 2. Continue collecting multiline proc signature args
        if active_proc is not None and collecting_proc_args:
            proc_args_parts.append(s.rstrip("\\").strip())
            proc_args_depth += _paren_delta(s)
            active_proc_last_line = line_no
            if proc_args_depth <= 0 and not s.endswith("\\"):
                combined_args = " ".join(p for p in proc_args_parts if p)
                if combined_args.endswith(")"):
                    combined_args = combined_args[:-1].strip()
                active_proc.args = combined_args
                collecting_proc_args = False
            prev_nonempty_line = line_no
            i += 1
            continue

        # 3. Continue multiline var initializer (e.g. list(\n...\n))
        if active_var_cont and active_var is not None:
            if indent == 0 and s.startswith("/"):
                active_var_cont = False
            else:
                cont_piece = s.rstrip("\\").strip()
                if cont_piece:
                    active_var.value = (
                        f"{active_var.value} {cont_piece}"
                        if active_var.value
                        else cont_piece
                    )
                active_var_paren_depth += _paren_delta(s)
                if active_var_paren_depth <= 0 and not s.endswith("\\"):
                    active_var_cont = False
                prev_nonempty_line = line_no
                i += 1
                continue

        # 4. Inside an active proc body?
        if active_proc is not None:
            if indent > active_proc_indent:
                active_proc_last_line = line_no
                prev_nonempty_line = line_no
                i += 1
                continue
            _close_active_proc()

        # 5. Pop stack for dedented lines
        _pop_stack_to(indent, prev_nonempty_line)
        prev_nonempty_line = line_no

        top = stack[-1] if stack else None
        cur_type = top.typepath if top else None
        cur_mode = top.mode if top else None

        # 6. Check for '=' at top level (outside strings/parens)
        eq_idx = _find_top_level_char(s, "=")

        # 7. Inline absolute var path: /obj/item/foo/var/bar = 1 or /var/x = 1
        if s.startswith("/") and "/var/" in s:
            lhs = s[:eq_idx].strip() if eq_idx != -1 else s
            if "(" not in lhs:
                owner_part, _, var_tail = lhs.partition("/var/")
                owner_tp = normalize_typepath(owner_part) if owner_part else "/"
                parsed_var = _parse_var_decl_lhs("var/" + var_tail)
                if parsed_var is not None:
                    vname, vtype = parsed_var
                    val = s[eq_idx + 1 :].strip() if eq_idx != -1 else None
                    vdef = VarDef(
                        name=vname,
                        value=val.rstrip("\\").strip() if val else None,
                        is_decl=True,
                        var_type=vtype,
                        file=rel_path,
                        line=line_no,
                    )
                    result.vars_by_type.setdefault(owner_tp, []).append(vdef)
                    if owner_tp != "/":
                        _record_type(owner_tp, line_no)
                    if val is not None:
                        pdepth = _paren_delta(val)
                        if pdepth > 0 or val.endswith("\\"):
                            active_var = vdef
                            active_var_paren_depth = max(0, pdepth)
                            active_var_cont = True
                    i += 1
                    continue

        # 8. Proc / Verb header: path(args...)
        pm = _RE_PROC_PREFIX.match(s)
        if pm and (eq_idx == -1 or s.find("(") < eq_idx):
            proc_path = pm.group(1)
            is_proc_or_verb_root = proc_path.startswith(
                ("/proc/", "/verb/", "proc/", "verb/")
            )
            can_be_proc = is_proc_or_verb_root or cur_mode in ("type", "proc", "verb") or proc_path.startswith("/")
            first_seg = proc_path.lstrip("/").split("/", 1)[0]
            if can_be_proc and (
                is_proc_or_verb_root or (first_seg not in _DM_KEYWORDS and first_seg != "var")
            ):
                resolved = _resolve_proc_owner_and_name(proc_path, cur_type, cur_mode)
                if resolved is not None:
                    owner_tp, proc_name, kind, is_new = resolved
                    open_paren = s.find("(", len(proc_path))
                    args_str, after_close, rem_depth = _split_proc_args(s, open_paren)
                    pdef = ProcDef(
                        typepath=owner_tp,
                        name=proc_name,
                        kind=kind,
                        is_new=is_new,
                        args=args_str,
                        file=rel_path,
                        line=line_no,
                        end_line=line_no,
                    )
                    result.procs.append(pdef)
                    if owner_tp != "/":
                        _record_type(owner_tp, line_no)

                    if rem_depth > 0 or s.endswith("\\"):
                        active_proc = pdef
                        active_proc_indent = indent
                        active_proc_last_line = line_no
                        collecting_proc_args = True
                        proc_args_depth = rem_depth
                        proc_args_parts = [args_str] if args_str else []
                    elif after_close and not after_close.startswith(";"):
                        # Single-line inline proc body
                        pdef.end_line = line_no
                        result.scopes.append(
                            ScopeSpan(
                                start_line=line_no,
                                end_line=line_no,
                                typepath=owner_tp,
                                proc_name=proc_name,
                            )
                        )
                    else:
                        active_proc = pdef
                        active_proc_indent = indent
                        active_proc_last_line = line_no
                        collecting_proc_args = False

                    i += 1
                    continue

        # 9. Block headers (var, VAR_PRIVATE, proc, verb, /type/proc, /type/verb)
        if eq_idx == -1 and "(" not in s:
            next_ind = next_nonempty_indent[i]
            # Bare var / VAR_* or var/prefix block when next line is indented deeper
            is_var_block = (
                s == "var"
                or s in _VAR_MACRO_PREFIXES
                or (
                    (s.startswith("var/") or any(s.startswith(m + "/") for m in _VAR_MACRO_PREFIXES))
                    and next_ind > indent
                )
            )
            if is_var_block and cur_type is not None:
                prefix = "" if s in ("var", *_VAR_MACRO_PREFIXES) else s
                stack.append(
                    _StackEntry(
                        indent=indent,
                        typepath=cur_type,
                        mode="var",
                        var_prefix=prefix,
                        start_line=line_no,
                    )
                )
                i += 1
                continue

            # Bare proc / verb block
            if s in ("proc", "verb") and cur_type is not None:
                stack.append(
                    _StackEntry(
                        indent=indent,
                        typepath=cur_type,
                        mode=s,
                        var_prefix="",
                        start_line=line_no,
                    )
                )
                i += 1
                continue

            # Absolute /type/proc or /type/verb block
            if s.startswith("/") and (s.endswith("/proc") or s.endswith("/verb")):
                mode_kw = "proc" if s.endswith("/proc") else "verb"
                owner_raw = s[: -(len(mode_kw) + 1)]
                owner_tp = normalize_typepath(owner_raw) if owner_raw else "/"
                if owner_tp != "/":
                    _record_type(owner_tp, line_no)
                stack.append(
                    _StackEntry(
                        indent=indent,
                        typepath=owner_tp,
                        mode=mode_kw,
                        var_prefix="",
                        start_line=line_no,
                    )
                )
                i += 1
                continue

        # 10. Var declaration or attribute assignment
        is_explicit_var_decl = (
            s.startswith("var/")
            or any(s.startswith(m + "/") for m in _VAR_MACRO_PREFIXES)
        )
        if (cur_mode == "var" and cur_type is not None) or is_explicit_var_decl:
            var_owner = cur_type if cur_type is not None else "/"
            lhs = s[:eq_idx].strip() if eq_idx != -1 else s
            val = s[eq_idx + 1 :].strip() if eq_idx != -1 else None
            prefix = top.var_prefix if (top and cur_mode == "var") else ""
            parsed_var = _parse_var_decl_lhs(lhs, prefix=prefix)
            if parsed_var is not None:
                vname, vtype = parsed_var
                vdef = VarDef(
                    name=vname,
                    value=val.rstrip("\\").strip() if val else None,
                    is_decl=True,
                    var_type=vtype,
                    file=rel_path,
                    line=line_no,
                )
                result.vars_by_type.setdefault(var_owner, []).append(vdef)
                if val is not None:
                    pdepth = _paren_delta(val)
                    if pdepth > 0 or val.endswith("\\"):
                        active_var = vdef
                        active_var_paren_depth = max(0, pdepth)
                        active_var_cont = True
                i += 1
                continue

        if eq_idx != -1 and cur_type is not None and cur_mode == "type":
            lhs = s[:eq_idx].strip()
            val = s[eq_idx + 1 :].strip()
            if _RE_IDENT.match(lhs):
                clean_val = val.rstrip("\\").strip()
                vdef = VarDef(
                    name=lhs,
                    value=clean_val,
                    is_decl=False,
                    var_type=None,
                    file=rel_path,
                    line=line_no,
                )
                result.vars_by_type.setdefault(cur_type, []).append(vdef)
                if lhs == "parent_type" and clean_val.startswith("/"):
                    result.parent_types[cur_type] = normalize_typepath(clean_val)
                pdepth = _paren_delta(val)
                if pdepth > 0 or val.endswith("\\"):
                    active_var = vdef
                    active_var_paren_depth = max(0, pdepth)
                    active_var_cont = True
                i += 1
                continue

        # 11. Typepath definitions (absolute or relative)
        if eq_idx == -1 and "(" not in s:
            if s.startswith("/") and _RE_TYPEPATH.match(s):
                tp = normalize_typepath(s)
                _record_type(tp, line_no)
                stack.append(
                    _StackEntry(
                        indent=indent,
                        typepath=tp,
                        mode="type",
                        var_prefix="",
                        start_line=line_no,
                    )
                )
                i += 1
                continue

            if _RE_REL_TYPE.match(s):
                first_seg = s.split("/", 1)[0]
                if first_seg not in _DM_KEYWORDS and s not in _VAR_MACRO_PREFIXES:
                    if cur_mode == "type" and cur_type is not None:
                        tp = normalize_typepath(f"{cur_type}/{s}")
                    elif cur_type is None and ("/" in s or first_seg in _DM_BUILTIN_ROOTS):
                        tp = normalize_typepath(f"/{s}")
                    else:
                        tp = ""
                    if tp:
                        _record_type(tp, line_no)
                        stack.append(
                            _StackEntry(
                                indent=indent,
                                typepath=tp,
                                mode="type",
                                var_prefix="",
                                start_line=line_no,
                            )
                        )
                        i += 1
                        continue

        i += 1

    _close_active_proc()
    _pop_stack_to(-1, prev_nonempty_line)
    return result
