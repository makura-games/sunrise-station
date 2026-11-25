"""FastMCP stdio server entrypoint for ss13-dm-mcp."""

from __future__ import annotations

from pathlib import Path
from typing import Any

try:
    from mcp.server.mcpserver import MCPServer as FastMCP
except ImportError:
    from mcp.server.fastmcp import FastMCP

from .config import RepoConfig, load_repo_config
from .dm_index import DmIndex
from .dmi import DmiInspector

mcp = FastMCP("ss13-dm-mcp")


class ServerContext:
    """Lazy-initialized per-repo DM and DMI indices."""

    def __init__(self) -> None:
        self._config: RepoConfig | None = None
        self._dm_indices: dict[Path, DmIndex] = {}
        self._dmi_inspectors: dict[Path, DmiInspector] = {}

    @property
    def config(self) -> RepoConfig:
        if self._config is None:
            self._config = load_repo_config()
        return self._config

    def reset_config(self) -> None:
        self._config = None
        self._dm_indices.clear()
        self._dmi_inspectors.clear()

    def get_dm_index(self, repo: str | None = None) -> DmIndex:
        name, root = self.config.resolve(repo)
        idx = self._dm_indices.get(root)
        if idx is None:
            idx = DmIndex(root, repo_name=name)
            self._dm_indices[root] = idx
        return idx

    def get_dmi_inspector(self, repo: str | None = None) -> DmiInspector:
        name, root = self.config.resolve(repo)
        insp = self._dmi_inspectors.get(root)
        if insp is None:
            insp = DmiInspector(root, repo_name=name)
            self._dmi_inspectors[root] = insp
        return insp


_CTX = ServerContext()


@mcp.tool()
def dm_get_type(
    typepath: str,
    include_inherited: bool = True,
    filter_prefix: str | None = None,
    repo: str | None = None,
) -> dict[str, Any]:
    """Inspect a DM typepath: all definition locations, resolved vars, and proc/verb signatures."""
    try:
        return _CTX.get_dm_index(repo).get_type(
            typepath=typepath,
            include_inherited=include_inherited,
            filter_prefix=filter_prefix,
        )
    except ValueError as exc:
        return {"error": str(exc)}


@mcp.tool()
def dm_get_proc(
    typepath: str,
    proc_name: str,
    include_parents: bool = False,
    repo: str | None = None,
) -> dict[str, Any]:
    """Return exact source code of a DM proc/verb (and optionally parent ..() implementations)."""
    try:
        return _CTX.get_dm_index(repo).get_proc(
            typepath=typepath,
            proc_name=proc_name,
            include_parents=include_parents,
        )
    except ValueError as exc:
        return {"error": str(exc)}


@mcp.tool()
def dm_search_defines(
    query: str,
    limit: int = 30,
    repo: str | None = None,
) -> dict[str, Any]:
    """Search #define macros by name with file and line numbers."""
    try:
        return _CTX.get_dm_index(repo).search_defines(query=query, limit=limit)
    except ValueError as exc:
        return {"error": str(exc)}


@mcp.tool()
def dm_search_code(
    pattern: str,
    subpath: str | None = None,
    limit: int = 30,
    repo: str | None = None,
) -> dict[str, Any]:
    """Fast regex search across .dm files returning file:line, enclosing type/proc, and context."""
    try:
        return _CTX.get_dm_index(repo).search_code(
            pattern=pattern,
            subpath=subpath,
            limit=limit,
        )
    except ValueError as exc:
        return {"error": str(exc)}


@mcp.tool()
def dmi_inspect(
    dmi_path: str,
    state: str | None = None,
    include_frame_hashes: bool = False,
    repo: str | None = None,
) -> dict[str, Any]:
    """Inspect a BYOND .dmi spritesheet's zTXt metadata and optionally SHA256 hashes of states/frames."""
    try:
        return _CTX.get_dmi_inspector(repo).inspect(
            dmi_path=dmi_path,
            state=state,
            include_frame_hashes=include_frame_hashes,
        )
    except ValueError as exc:
        return {"error": str(exc)}


def main() -> None:
    """Run the ss13-dm-mcp server over stdio."""
    mcp.run(transport="stdio")


if __name__ == "__main__":
    main()
