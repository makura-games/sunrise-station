"""Prototype inheritance resolution, partial patching, querying, and tree inspection."""

from __future__ import annotations

import copy
from collections import deque
from dataclasses import dataclass
from typing import Any

from .proto_index import ProtoIndex, PrototypeRecord

_RESERVED_TOP_KEYS = {"type", "id", "parent", "abstract", "components"}


@dataclass(slots=True)
class _ProtoSummary:
    proto_id: str
    proto_type: str
    parents: list[str]
    file: str
    line: int
    abstract: bool
    components: list[str]
    tags: set[str]


def _is_op(val: Any, op_name: str) -> bool:
    return isinstance(val, dict) and val.get("__op__") == op_name


def _strip_op_wrappers(val: Any) -> Any:
    """Clean standalone op wrappers when no prior value exists."""
    if isinstance(val, dict):
        op = val.get("__op__")
        if op == "remove":
            return None
        if op == "clear":
            inner = val.get("__value__")
            if isinstance(inner, list):
                out: list[Any] = []
                _apply_list_patch(out, inner)
                return out
            if isinstance(inner, dict):
                return {k: _strip_op_wrappers(v) for k, v in inner.items()}
            return [] if inner is None else _strip_op_wrappers(inner)
        if op == "index":
            return _strip_op_wrappers(val.get("__value__"))
        return {k: _strip_op_wrappers(v) for k, v in val.items()}
    if isinstance(val, list):
        return [_strip_op_wrappers(item) for item in val if not _is_op(item, "remove")]
    return copy.deepcopy(val)


def _apply_list_patch(target: list[Any], patch_items: list[Any]) -> None:
    """Apply SS14 partial list operations (!Remove, !Index:N) or append items."""
    for item in patch_items:
        if _is_op(item, "remove"):
            rem_val = item.get("__value__")
            target[:] = [x for x in target if x != rem_val]
        elif _is_op(item, "index"):
            idx = item.get("__index__", 0)
            val = _strip_op_wrappers(item.get("__value__"))
            if isinstance(idx, int):
                if idx < 0:
                    insert_at = max(0, len(target) + idx + 1)
                else:
                    insert_at = min(idx, len(target))
                target.insert(insert_at, val)
            else:
                target.append(val)
        else:
            target.append(_strip_op_wrappers(item))


def _contains_partial_list_ops(items: list[Any]) -> bool:
    return any(_is_op(item, "remove") or _is_op(item, "index") for item in items)


def _merge_value(
    old: Any,
    new: Any,
    path: str,
    warnings: list[str],
    *,
    is_partial: bool,
    source_loc: str = "",
) -> Any:
    """Merge new value onto old value, detecting parent list overwrites."""
    if _is_op(new, "remove"):
        return None

    if _is_op(new, "clear"):
        inner = new.get("__value__")
        if isinstance(old, list) and old:
            suffix = f" (via partial {source_loc})" if is_partial and source_loc else ""
            warnings.append(f"{path} clears parent values {old!r}{suffix}")
        if isinstance(inner, list):
            result: list[Any] = []
            _apply_list_patch(result, inner)
            return result
        if isinstance(inner, dict):
            return {k: _strip_op_wrappers(v) for k, v in inner.items()}
        if isinstance(old, dict):
            return {}
        return []

    if isinstance(old, dict) and isinstance(new, dict) and "__tag__" not in new:
        merged = copy.deepcopy(old)
        for key, val in new.items():
            if _is_op(val, "remove") and val.get("__value__") is None:
                merged.pop(key, None)
                continue
            sub_path = f"{path}.{key}"
            if key in merged:
                updated = _merge_value(
                    merged[key],
                    val,
                    sub_path,
                    warnings,
                    is_partial=is_partial,
                    source_loc=source_loc,
                )
                if updated is None and _is_op(val, "remove"):
                    merged.pop(key, None)
                else:
                    merged[key] = updated
            else:
                cleaned = _strip_op_wrappers(val)
                if cleaned is not None:
                    merged[key] = cleaned
        return merged

    if isinstance(old, list) and isinstance(new, list):
        if is_partial and _contains_partial_list_ops(new):
            merged_list = copy.deepcopy(old)
            _apply_list_patch(merged_list, new)
            return merged_list
        cleaned_new = [_strip_op_wrappers(x) for x in new]
        if old and old != cleaned_new:
            suffix = f" (via partial {source_loc})" if is_partial and source_loc else ""
            warnings.append(
                f"{path} overrides parent values {old!r} -> {cleaned_new!r}{suffix}"
            )
        return cleaned_new

    return _strip_op_wrappers(new)


def _merge_component(
    target: dict[str, Any],
    incoming: dict[str, Any],
    comp_type: str,
    warnings: list[str],
    *,
    is_partial: bool,
    source_loc: str = "",
) -> dict[str, Any]:
    merged = copy.deepcopy(target)
    for key, val in incoming.items():
        if key == "type":
            continue
        if _is_op(val, "remove") and val.get("__value__") is None:
            merged.pop(key, None)
            continue
        field_path = f"{comp_type}.{key}"
        if key in merged:
            updated = _merge_value(
                merged[key],
                val,
                field_path,
                warnings,
                is_partial=is_partial,
                source_loc=source_loc,
            )
            if updated is None and _is_op(val, "remove"):
                merged.pop(key, None)
            else:
                merged[key] = updated
        else:
            cleaned = _strip_op_wrappers(val)
            if cleaned is not None:
                merged[key] = cleaned
    return merged


class PrototypeResolver:
    """Resolves SS14 prototypes with multiple inheritance, partials, and list warnings."""

    def __init__(self, index: ProtoIndex) -> None:
        self.index = index
        self._summary_gen: int = -1
        self._summary_cache: dict[tuple[str, str], _ProtoSummary] = {}

    def _suggest_ids(self, proto_id: str, proto_type: str, limit: int = 8) -> list[str]:
        needle = proto_id.lower()
        exact_other = [
            f"{rec.proto_type}:{rec.proto_id}"
            for rec in self.index.by_id.get(proto_id, [])
            if rec.proto_type != proto_type
        ]
        if exact_other:
            return exact_other[:limit]

        matches: list[str] = []
        for (p_type, p_id) in self.index.definitions:
            if p_type == proto_type and needle in p_id.lower():
                matches.append(p_id)
                if len(matches) >= limit:
                    break
        return matches

    def _collect_merge_order(
        self,
        proto_type: str,
        proto_id: str,
        visiting: set[str],
        visited: set[str],
        order: list[PrototypeRecord],
        missing_parents: list[str],
    ) -> None:
        if proto_id in visited:
            return
        if proto_id in visiting:
            return
        rec = self.index.definitions.get((proto_type, proto_id))
        if rec is None:
            missing_parents.append(proto_id)
            return

        visiting.add(proto_id)
        for parent_id in rec.parents:
            self._collect_merge_order(
                proto_type, parent_id, visiting, visited, order, missing_parents
            )
        visiting.remove(proto_id)
        visited.add(proto_id)
        order.append(rec)

    def _resolve_record_chain(
        self, proto_id: str, proto_type: str
    ) -> tuple[
        PrototypeRecord | None,
        list[PrototypeRecord],
        list[str],
        dict[str, dict[str, Any]],
        dict[str, list[str]],
        dict[str, Any],
        list[str],
        list[str],
    ]:
        root_rec = self.index.definitions.get((proto_type, proto_id))
        if root_rec is None:
            return None, [], [], {}, {}, {}, [], []

        order: list[PrototypeRecord] = []
        missing_parents: list[str] = []
        self._collect_merge_order(
            proto_type, proto_id, set(), set(), order, missing_parents
        )

        components_by_type: dict[str, dict[str, Any]] = {}
        component_sources: dict[str, list[str]] = {}
        fields: dict[str, Any] = {}
        partials_applied: list[str] = []
        warnings: list[str] = []

        for mp in missing_parents:
            warnings.append(f"Missing parent prototype '{mp}' (type={proto_type})")

        for rec in order:
            self._apply_record(
                rec,
                components_by_type,
                component_sources,
                fields,
                warnings,
                is_partial=False,
            )
            for partial_rec in self.index.partials.get(
                (rec.proto_type, rec.proto_id), []
            ):
                partials_applied.append(f"{partial_rec.file}:{partial_rec.line}")
                self._apply_record(
                    partial_rec,
                    components_by_type,
                    component_sources,
                    fields,
                    warnings,
                    is_partial=True,
                )

        # Deduplicate warnings preserving order.
        dedup_warnings = list(dict.fromkeys(warnings))
        return (
            root_rec,
            order,
            partials_applied,
            components_by_type,
            component_sources,
            fields,
            dedup_warnings,
            missing_parents,
        )

    @staticmethod
    def _apply_record(
        rec: PrototypeRecord,
        components_by_type: dict[str, dict[str, Any]],
        component_sources: dict[str, list[str]],
        fields: dict[str, Any],
        warnings: list[str],
        *,
        is_partial: bool,
    ) -> None:
        loc = f"{rec.file}:{rec.line}"
        for key, val in rec.raw.items():
            if key in _RESERVED_TOP_KEYS:
                continue
            if _is_op(val, "remove") and val.get("__value__") is None:
                fields.pop(key, None)
                continue
            if key in fields:
                updated = _merge_value(
                    fields[key],
                    val,
                    key,
                    warnings,
                    is_partial=is_partial,
                    source_loc=loc,
                )
                if updated is None and _is_op(val, "remove"):
                    fields.pop(key, None)
                else:
                    fields[key] = updated
            else:
                cleaned = _strip_op_wrappers(val)
                if cleaned is not None:
                    fields[key] = cleaned

        raw_comps = rec.raw.get("components")
        if not isinstance(raw_comps, list):
            return

        for comp in raw_comps:
            if not isinstance(comp, dict):
                continue
            comp_type = comp.get("type")
            if not isinstance(comp_type, str) or not comp_type:
                continue

            if comp_type in components_by_type:
                components_by_type[comp_type] = _merge_component(
                    components_by_type[comp_type],
                    comp,
                    comp_type,
                    warnings,
                    is_partial=is_partial,
                    source_loc=loc,
                )
            else:
                cleaned_comp: dict[str, Any] = {"type": comp_type}
                for ck, cv in comp.items():
                    if ck == "type":
                        continue
                    cleaned_val = _strip_op_wrappers(cv)
                    if cleaned_val is not None:
                        cleaned_comp[ck] = cleaned_val
                components_by_type[comp_type] = cleaned_comp

            sources = component_sources.setdefault(comp_type, [])
            if loc not in sources:
                sources.append(loc)

    def resolve_prototype(
        self,
        proto_id: str,
        proto_type: str = "entity",
        components: list[str] | None = None,
    ) -> dict[str, Any]:
        """Resolve a prototype with full inheritance, partial patches, and warnings."""
        self.index.ensure_fresh()
        (
            root_rec,
            order,
            partials_applied,
            components_by_type,
            component_sources,
            fields,
            warnings,
            _,
        ) = self._resolve_record_chain(proto_id, proto_type)

        if root_rec is None:
            return {
                "error": f"Prototype '{proto_id}' (type='{proto_type}') not found.",
                "proto_id": proto_id,
                "proto_type": proto_type,
                "suggestions": self._suggest_ids(proto_id, proto_type),
            }

        if components is not None:
            wanted = {c.lower() for c in components}
            wanted.add("tag")
            filtered_comps = [
                comp
                for name, comp in components_by_type.items()
                if name.lower() in wanted
            ]
            filtered_sources = {
                name: srcs
                for name, srcs in component_sources.items()
                if name.lower() in wanted
            }
        else:
            filtered_comps = list(components_by_type.values())
            filtered_sources = component_sources

        return {
            "id": root_rec.proto_id,
            "type": root_rec.proto_type,
            "abstract": root_rec.abstract,
            "source": f"{root_rec.file}:{root_rec.line}",
            "parents": root_rec.parents,
            "parent_chain": [
                {"id": r.proto_id, "source": f"{r.file}:{r.line}"} for r in order[:-1]
            ],
            "partials_applied": partials_applied,
            "fields": fields,
            "components": filtered_comps,
            "component_sources": filtered_sources,
            "warnings": warnings,
        }

    def _get_summary(self, proto_type: str, proto_id: str) -> _ProtoSummary | None:
        if self._summary_gen != self.index.generation:
            self._summary_cache.clear()
            self._summary_gen = self.index.generation

        key = (proto_type, proto_id)
        cached = self._summary_cache.get(key)
        if cached is not None:
            return cached

        root_rec = self.index.definitions.get(key)
        if root_rec is None:
            return None

        (_, _, _, comps_by_type, _, _, _, _) = self._resolve_record_chain(
            proto_id, proto_type
        )
        tags: set[str] = set()
        tag_comp = comps_by_type.get("Tag")
        if isinstance(tag_comp, dict):
            raw_tags = tag_comp.get("tags")
            if isinstance(raw_tags, list):
                for t in raw_tags:
                    if isinstance(t, str):
                        tags.add(t)

        summary = _ProtoSummary(
            proto_id=root_rec.proto_id,
            proto_type=root_rec.proto_type,
            parents=root_rec.parents,
            file=root_rec.file,
            line=root_rec.line,
            abstract=root_rec.abstract,
            components=list(comps_by_type.keys()),
            tags=tags,
        )
        self._summary_cache[key] = summary
        return summary

    def _descendant_ids(self, proto_type: str, parent_id: str) -> set[str]:
        descendants: set[str] = set()
        queue: deque[str] = deque([parent_id])
        while queue:
            cur = queue.popleft()
            for child_id in self.index.children_map.get((proto_type, cur), []):
                if child_id not in descendants:
                    descendants.add(child_id)
                    queue.append(child_id)
        return descendants

    def query_prototypes(
        self,
        component: str | None = None,
        tag: str | None = None,
        parent: str | None = None,
        id_contains: str | None = None,
        path_prefix: str | None = None,
        include_abstract: bool = False,
        limit: int = 30,
    ) -> dict[str, Any]:
        """Query entity prototypes by inherited component, tag, parent, ID, or path."""
        self.index.ensure_fresh()
        clamped_limit = max(1, min(limit, 200))
        proto_type = "entity"

        norm_prefix = (
            path_prefix.strip().replace("\\", "/").lstrip("/").lower()
            if path_prefix
            else None
        )
        norm_id = id_contains.lower() if id_contains else None
        norm_comp = component.lower() if component else None
        norm_tag = tag.lower() if tag else None

        if parent:
            candidate_ids = sorted(self._descendant_ids(proto_type, parent))
            candidates = [
                self.index.definitions[(proto_type, pid)]
                for pid in candidate_ids
                if (proto_type, pid) in self.index.definitions
            ]
        else:
            candidates = [
                rec
                for (ptype, _), rec in sorted(self.index.definitions.items())
                if ptype == proto_type
            ]

        matched: list[dict[str, Any]] = []
        total_matched = 0

        for rec in candidates:
            if not include_abstract and rec.abstract:
                continue
            if norm_id and norm_id not in rec.proto_id.lower():
                continue
            if norm_prefix and not rec.file.lower().startswith(norm_prefix):
                continue

            summary = self._get_summary(proto_type, rec.proto_id)
            if summary is None:
                continue

            if norm_comp and not any(c.lower() == norm_comp for c in summary.components):
                continue
            if norm_tag and not any(t.lower() == norm_tag for t in summary.tags):
                continue

            total_matched += 1
            if len(matched) < clamped_limit:
                matched.append(
                    {
                        "id": summary.proto_id,
                        "parent": summary.parents[0]
                        if len(summary.parents) == 1
                        else summary.parents,
                        "file": summary.file,
                        "line": summary.line,
                        "abstract": summary.abstract,
                        "components": summary.components,
                    }
                )

        return {
            "total_matched": total_matched,
            "returned": len(matched),
            "truncated": total_matched > len(matched),
            "items": matched,
        }

    def get_inheritance_tree(
        self,
        proto_id: str,
        direction: str = "both",
        max_depth: int = 5,
        proto_type: str = "entity",
    ) -> dict[str, Any]:
        """Return ancestor and/or descendant tree for a prototype."""
        self.index.ensure_fresh()
        if direction not in ("up", "down", "both"):
            return {
                "error": f"Invalid direction '{direction}'. Expected 'up', 'down', or 'both'."
            }

        clamped_depth = max(1, min(max_depth, 15))
        root_rec = self.index.definitions.get((proto_type, proto_id))
        if root_rec is None:
            return {
                "error": f"Prototype '{proto_id}' (type='{proto_type}') not found.",
                "proto_id": proto_id,
                "proto_type": proto_type,
                "suggestions": self._suggest_ids(proto_id, proto_type),
            }

        result: dict[str, Any] = {
            "id": root_rec.proto_id,
            "type": root_rec.proto_type,
            "abstract": root_rec.abstract,
            "source": f"{root_rec.file}:{root_rec.line}",
        }

        if direction in ("up", "both"):
            result["ancestors"] = self._build_up_tree(
                proto_type, proto_id, clamped_depth, set()
            )

        if direction in ("down", "both"):
            counter = {"count": 0, "truncated": False}
            result["descendants"] = self._build_down_tree(
                proto_type, proto_id, clamped_depth, set(), counter, max_nodes=200
            )
            result["descendants_truncated"] = counter["truncated"]

        return result

    def _build_up_tree(
        self,
        proto_type: str,
        proto_id: str,
        remaining_depth: int,
        visiting: set[str],
    ) -> list[dict[str, Any]]:
        if remaining_depth <= 0 or proto_id in visiting:
            return []
        rec = self.index.definitions.get((proto_type, proto_id))
        if rec is None:
            return []

        visiting.add(proto_id)
        nodes: list[dict[str, Any]] = []
        for parent_id in rec.parents:
            p_rec = self.index.definitions.get((proto_type, parent_id))
            if p_rec is None:
                nodes.append({"id": parent_id, "missing": True})
                continue
            node: dict[str, Any] = {
                "id": p_rec.proto_id,
                "abstract": p_rec.abstract,
                "source": f"{p_rec.file}:{p_rec.line}",
            }
            if remaining_depth > 1 and p_rec.parents:
                node["parents"] = self._build_up_tree(
                    proto_type, p_rec.proto_id, remaining_depth - 1, visiting
                )
            nodes.append(node)
        visiting.remove(proto_id)
        return nodes

    def _build_down_tree(
        self,
        proto_type: str,
        proto_id: str,
        remaining_depth: int,
        visiting: set[str],
        counter: dict[str, Any],
        max_nodes: int,
    ) -> list[dict[str, Any]]:
        if remaining_depth <= 0 or proto_id in visiting:
            return []
        children = sorted(self.index.children_map.get((proto_type, proto_id), []))
        if not children:
            return []

        visiting.add(proto_id)
        nodes: list[dict[str, Any]] = []
        for child_id in children:
            if counter["count"] >= max_nodes:
                counter["truncated"] = True
                break
            c_rec = self.index.definitions.get((proto_type, child_id))
            if c_rec is None:
                continue
            counter["count"] += 1
            node: dict[str, Any] = {
                "id": c_rec.proto_id,
                "abstract": c_rec.abstract,
                "source": f"{c_rec.file}:{c_rec.line}",
            }
            if remaining_depth > 1:
                sub = self._build_down_tree(
                    proto_type,
                    child_id,
                    remaining_depth - 1,
                    visiting,
                    counter,
                    max_nodes,
                )
                if sub:
                    node["children"] = sub
            nodes.append(node)
        visiting.remove(proto_id)
        return nodes
