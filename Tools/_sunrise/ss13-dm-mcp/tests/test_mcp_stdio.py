"""End-to-end stdio MCP client smoke test for all 5 ss13-dm-mcp tools."""

from __future__ import annotations

import asyncio
import json
import os
import sys
from pathlib import Path

import pytest
from mcp.client.session import ClientSession
from mcp.client.stdio import StdioServerParameters, stdio_client

def _get_ss13_repo() -> Path | None:
    raw = os.environ.get("SS13_REPO_PATH", "").strip()
    if not raw:
        return None
    p = Path(raw).expanduser().resolve()
    return p if p.is_dir() else None


_SS13_REPO = _get_ss13_repo()


def _parse_tool_json(result) -> dict:
    is_err = getattr(result, "is_error", getattr(result, "isError", False))
    assert not is_err, f"Tool returned error: {result.content}"
    text = "".join(block.text for block in result.content if hasattr(block, "text"))
    return json.loads(text)


async def _run_stdio_smoke(ss13_repo: Path) -> None:
    project_root = Path(__file__).resolve().parent.parent
    env = dict(os.environ)
    env["SS13_REPO_PATH"] = str(ss13_repo)

    params = StdioServerParameters(
        command=sys.executable,
        args=["-m", "ss13_dm_mcp.server"],
        cwd=str(project_root),
        env=env,
    )

    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            tools_resp = await session.list_tools()
            tool_names = {t.name for t in tools_resp.tools}
            expected = {
                "dm_get_type",
                "dm_get_proc",
                "dm_search_defines",
                "dm_search_code",
                "dmi_inspect",
            }
            assert expected == tool_names, f"Tool mismatch: {tool_names}"

            r1 = _parse_tool_json(
                await session.call_tool(
                    "dm_get_type",
                    {"typepath": "/mob/living/silicon/robot/cogscarab"},
                )
            )
            assert r1["typepath"] == "/mob/living/silicon/robot/cogscarab"
            assert r1["counts"]["vars"] > 0

            r2 = _parse_tool_json(
                await session.call_tool(
                    "dm_get_proc",
                    {
                        "typepath": "/mob/living/simple_animal/hostile/clockwork/marauder",
                        "proc_name": "deflect_projectile",
                    },
                )
            )
            assert r2["count"] == 1

            r3 = _parse_tool_json(
                await session.call_tool(
                    "dm_search_defines",
                    {"query": "CLOCK_MAX_WIND_UP_TIMER", "limit": 5},
                )
            )
            assert r3["total_matched"] >= 1

            r4 = _parse_tool_json(
                await session.call_tool(
                    "dm_search_code",
                    {
                        "pattern": "CLOCK_MAX_WIND_UP_TIMER",
                        "subpath": "code/game/gamemodes/clockwork",
                        "limit": 10,
                    },
                )
            )
            assert r4["returned"] >= 1

            r5 = _parse_tool_json(
                await session.call_tool(
                    "dmi_inspect",
                    {
                        "dmi_path": "icons/obj/clockwork.dmi",
                        "state": "clock_slab",
                        "include_frame_hashes": True,
                    },
                )
            )
            assert r5["states"][0]["frames"] == 15
            assert "state_sha256" in r5["states"][0]


@pytest.mark.skipif(_SS13_REPO is None, reason="Set SS13_REPO_PATH to run SS13 repo smoke tests")
def test_mcp_stdio_all_5_tools() -> None:
    assert _SS13_REPO is not None
    asyncio.run(_run_stdio_smoke(_SS13_REPO))
