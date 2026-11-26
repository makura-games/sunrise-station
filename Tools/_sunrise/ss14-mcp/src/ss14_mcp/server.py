"""FastMCP stdio server entrypoint for ss14-mcp."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

try:
    from mcp.server.mcpserver import MCPServer as FastMCP
except ImportError:
    from mcp.server.fastmcp import FastMCP

from .config import find_repo_root
from .cs_component import ComponentIndex
from .ftl_audit import FtlIndex, audit_ftl
from .proto_index import ProtoIndex
from .resolver import PrototypeResolver
from .rsi_audit import audit_rsi
from .upstream_hygiene import check_upstream_hygiene

mcp = FastMCP("ss14-mcp")


def _compact(data: dict[str, Any]) -> str:
    """Serialize tool output as compact single-line JSON to save context."""
    return json.dumps(data, ensure_ascii=False, separators=(",", ":"))


class ServerContext:
    """Lazy-initialized indices so server startup stays under 0.3s."""

    def __init__(self, repo_root: Path | None = None) -> None:
        self._repo_root = repo_root
        self._proto_index: ProtoIndex | None = None
        self._resolver: PrototypeResolver | None = None
        self._comp_index: ComponentIndex | None = None
        self._ftl_index: FtlIndex | None = None

    @property
    def repo_root(self) -> Path:
        if self._repo_root is None:
            self._repo_root = find_repo_root()
        return self._repo_root

    @property
    def proto_index(self) -> ProtoIndex:
        if self._proto_index is None:
            self._proto_index = ProtoIndex(self.repo_root)
        return self._proto_index

    @property
    def resolver(self) -> PrototypeResolver:
        if self._resolver is None:
            self._resolver = PrototypeResolver(self.proto_index)
        return self._resolver

    @property
    def comp_index(self) -> ComponentIndex:
        if self._comp_index is None:
            self._comp_index = ComponentIndex(self.repo_root)
        return self._comp_index

    @property
    def ftl_index(self) -> FtlIndex:
        if self._ftl_index is None:
            self._ftl_index = FtlIndex(self.repo_root)
        return self._ftl_index


_CTX = ServerContext()


@mcp.tool()
def ss14_resolve_prototype(
    proto_id: str,
    proto_type: str = "entity",
    components: list[str] | None = None,
) -> str:
    """Resolve an SS14 YAML prototype with full inheritance, _Sunrise/Partial patches, and list-overwrite warnings. When components=[...] is provided, returns requested components plus Tag automatically."""
    return _compact(
        _CTX.resolver.resolve_prototype(
            proto_id=proto_id,
            proto_type=proto_type,
            components=components,
        )
    )


@mcp.tool()
def ss14_query_prototypes(
    component: str | None = None,
    tag: str | None = None,
    parent: str | None = None,
    id_contains: str | None = None,
    path_prefix: str | None = None,
    include_abstract: bool = False,
    limit: int = 30,
) -> str:
    """Query entity prototypes by inherited component, Tag, parent ancestry, ID substring, or file path prefix."""
    return _compact(
        _CTX.resolver.query_prototypes(
            component=component,
            tag=tag,
            parent=parent,
            id_contains=id_contains,
            path_prefix=path_prefix,
            include_abstract=include_abstract,
            limit=limit,
        )
    )


@mcp.tool()
def ss14_get_inheritance_tree(
    proto_id: str,
    direction: str = "both",
    max_depth: int = 5,
    proto_type: str = "entity",
) -> str:
    """Return ancestor and/or descendant inheritance tree for an SS14 prototype."""
    return _compact(
        _CTX.resolver.get_inheritance_tree(
            proto_id=proto_id,
            direction=direction,
            max_depth=max_depth,
            proto_type=proto_type,
        )
    )


@mcp.tool()
def ss14_inspect_component(component_name: str) -> str:
    """Inspect a C# *Component class: [DataField] fields, C# types, defaults, networking, AutoGenerateComponentState args, and file locations."""
    return _compact(_CTX.comp_index.inspect_component(component_name))


@mcp.tool()
def ss14_audit_rsi(
    rsi_path: str = "",
    check_attributions: bool = True,
    audio_paths: list[str] | None = None,
    audio_dir: str | None = None,
    verbose: bool = False,
) -> str:
    """Audit an .rsi directory (compact summary by default; pass verbose=True for full states list) and/or an audio directory (via audio_dir or by passing an audio folder in rsi_path) against attributions.yml."""
    return _compact(
        audit_rsi(
            repo_root=_CTX.repo_root,
            rsi_path=rsi_path,
            check_attributions=check_attributions,
            audio_paths=audio_paths,
            audio_dir=audio_dir,
            verbose=verbose,
        )
    )


@mcp.tool()
def ss14_audit_ftl(
    path_prefix: str | None = None,
    query: str | None = None,
    proto_path_prefix: str | None = None,
    cs_path_prefix: str | None = None,
    limit: int = 50,
) -> str:
    """Audit ru-RU <-> en-US FTL parity, prototype ent-* coverage, and C# Loc.GetString references. Prefer query='<keyword>' (case-insensitive, -/_ normalized) to match all related locale folders (e.g. _prototypes and _strings) in one call; path_prefix returns matched_dirs suggestions on zero hits."""
    return _compact(
        audit_ftl(
            repo_root=_CTX.repo_root,
            proto_index=_CTX.proto_index,
            ftl_index=_CTX.ftl_index,
            path_prefix=path_prefix,
            query=query,
            proto_path_prefix=proto_path_prefix,
            cs_path_prefix=cs_path_prefix,
            limit=limit,
        )
    )


@mcp.tool()
def ss14_check_upstream_hygiene(base_ref: str | None = None) -> str:
    """Read-only audit of git changes outside _Sunrise and compliance with AGENTS.md Sunrise-Edit markers (checked for .cs/.yml/.yaml/.ftl/.xaml; non-code untracked files go to untracked_other)."""
    return _compact(
        check_upstream_hygiene(repo_root=_CTX.repo_root, base_ref=base_ref)
    )


def main() -> None:
    """Run the ss14-mcp server over stdio."""
    mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
