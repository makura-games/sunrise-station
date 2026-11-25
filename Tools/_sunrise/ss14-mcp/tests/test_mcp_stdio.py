"""End-to-end stdio MCP client smoke test for all 7 tools."""

from __future__ import annotations

import asyncio
import json
import sys
from pathlib import Path

from mcp.client.session import ClientSession
from mcp.client.stdio import StdioServerParameters, stdio_client


def _parse_tool_json(result) -> dict:
    is_err = getattr(result, "is_error", getattr(result, "isError", False))
    assert not is_err, f"Tool returned error: {result.content}"
    text = "".join(
        block.text for block in result.content if hasattr(block, "text")
    )
    return json.loads(text)


async def _run_stdio_smoke() -> None:
    project_root = Path(__file__).resolve().parent.parent
    params = StdioServerParameters(
        command=sys.executable,
        args=["-m", "ss14_mcp.server"],
        cwd=str(project_root),
    )

    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            tools_resp = await session.list_tools()
            tool_names = {t.name for t in tools_resp.tools}
            expected = {
                "ss14_resolve_prototype",
                "ss14_query_prototypes",
                "ss14_get_inheritance_tree",
                "ss14_inspect_component",
                "ss14_audit_rsi",
                "ss14_audit_ftl",
                "ss14_check_upstream_hygiene",
            }
            assert expected.issubset(tool_names), f"Missing tools: {expected - tool_names}"

            r1 = _parse_tool_json(
                await session.call_tool("ss14_resolve_prototype", {"proto_id": "SheetBrass"})
            )
            assert r1["id"] == "SheetBrass"

            r2 = _parse_tool_json(
                await session.call_tool(
                    "ss14_query_prototypes",
                    {"tag": "SunriseClockworkBrassSheet", "limit": 5},
                )
            )
            assert r2["total_matched"] >= 1

            r3 = _parse_tool_json(
                await session.call_tool(
                    "ss14_get_inheritance_tree",
                    {"proto_id": "BaseBorgChassisNT", "direction": "up", "max_depth": 3},
                )
            )
            assert r3["id"] == "BaseBorgChassisNT"
            assert len(r3["ancestors"]) == 2

            r4 = _parse_tool_json(
                await session.call_tool(
                    "ss14_inspect_component", {"component_name": "BorgMagboots"}
                )
            )
            assert r4["component"] == "BorgMagbootsComponent"

            r5 = _parse_tool_json(
                await session.call_tool(
                    "ss14_audit_rsi",
                    {"rsi_path": "_Sunrise/Abductor/Clothing/Back/Duffels/abductor.rsi"},
                )
            )
            assert r5["states_in_meta"] == 4
            assert "states" not in r5

            r6 = _parse_tool_json(
                await session.call_tool(
                    "ss14_audit_ftl", {"query": "clockwork", "limit": 10}
                )
            )
            assert r6["counts"]["ru_keys"] > 0
            assert len(r6["matched_dirs"]) >= 1

            r7 = _parse_tool_json(
                await session.call_tool("ss14_check_upstream_hygiene", {})
            )
            assert "counts" in r7


def test_mcp_stdio_all_7_tools() -> None:
    asyncio.run(_run_stdio_smoke())
