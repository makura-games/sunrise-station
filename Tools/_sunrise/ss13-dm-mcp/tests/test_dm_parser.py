"""Unit tests and Paradise-master220 smoke tests for DM parser, index, and config."""

from __future__ import annotations

import json
import os
import time
from pathlib import Path

import pytest

from ss13_dm_mcp.config import load_repo_config, safe_resolve_under_repo
from ss13_dm_mcp.dm_index import DmIndex
from ss13_dm_mcp.dm_parser import parse_dm_file

def _get_ss13_repo() -> Path | None:
    raw = os.environ.get("SS13_REPO_PATH", "").strip()
    if not raw:
        return None
    p = Path(raw).expanduser().resolve()
    return p if p.is_dir() else None


_SS13_REPO = _get_ss13_repo()


def test_parse_indentation_relative_paths_and_comments() -> None:
    dm = """
// Top comment with /fake/type
#define MAX_CHARGE 42
#define ADD_ONE(x) ((x) + 1)
#define MULTILINE_MACRO(a, b) \\
    ((a) + (b))

/obj/item/clockwork
\tname = "clockwork base"
\tdesc = {"
\t/fake/type/inside_ml_string
\tvar/fake_var = 1
\t"}
\t/* outer /* nested */ comment /fake/nested
\tvar/should_not_exist = 99
\t*/
\tvar/url = "https://example.com//not_a_comment"

\tsoul_vessel
\t\tname = "soul vessel"
\t\tvar/tmp/charge = MAX_CHARGE

\t\tattack_self(mob/user)
\t\t\t// comment inside proc
\t\t\treturn TRUE

\t\tproc/drain(amount = 5)
\t\t\tcharge -= amount
"""
    parsed = parse_dm_file("code/test.dm", dm)
    assert "/obj/item/clockwork" in parsed.type_locations
    assert "/obj/item/clockwork/soul_vessel" in parsed.type_locations
    assert "/fake/type" not in parsed.type_locations
    assert "/fake/type/inside_ml_string" not in parsed.type_locations
    assert "/fake/nested" not in parsed.type_locations

    base_vars = {v.name: v for v in parsed.vars_by_type["/obj/item/clockwork"]}
    assert "should_not_exist" not in base_vars
    assert base_vars["url"].value == '"https://example.com//not_a_comment"'

    sv_vars = {v.name: v for v in parsed.vars_by_type["/obj/item/clockwork/soul_vessel"]}
    assert sv_vars["charge"].is_decl is True
    assert sv_vars["charge"].value == "MAX_CHARGE"

    procs = {(p.typepath, p.name): p for p in parsed.procs}
    assert ("/obj/item/clockwork/soul_vessel", "attack_self") in procs
    assert procs[("/obj/item/clockwork/soul_vessel", "attack_self")].kind == "override"
    assert ("/obj/item/clockwork/soul_vessel", "drain") in procs
    assert procs[("/obj/item/clockwork/soul_vessel", "drain")].kind == "proc"
    assert procs[("/obj/item/clockwork/soul_vessel", "drain")].args == "amount = 5"

    defines = {d.name: d for d in parsed.defines}
    assert defines["MAX_CHARGE"].params is None
    assert defines["MAX_CHARGE"].value == "42"
    assert defines["ADD_ONE"].params == "x"
    assert defines["MULTILINE_MACRO"].params == "a, b"
    assert defines["MULTILINE_MACRO"].value == "((a) + (b))"


def test_parse_var_proc_blocks_and_global_procs() -> None:
    dm = """
/proc/helper_fn(x)
\treturn x * 2

/datum/widget
\tvar
\t\talpha = 1
\t\tlist/items = list(
\t\t\t"a",
\t\t\t"b"
\t\t)
\tVAR_PRIVATE
\t\tsecret = 7
\tverb
\t\tinspect_widget()
\t\t\treturn alpha
"""
    parsed = parse_dm_file("code/widget.dm", dm)
    wvars = {v.name: v for v in parsed.vars_by_type["/datum/widget"]}
    assert wvars["alpha"].is_decl is True
    assert wvars["items"].var_type == "/list"
    assert '"b"' in (wvars["items"].value or "")
    assert wvars["secret"].is_decl is True

    procs = {(p.typepath, p.name): p for p in parsed.procs}
    assert ("/", "helper_fn") in procs
    assert procs[("/", "helper_fn")].kind == "proc"
    assert ("/datum/widget", "inspect_widget") in procs
    assert procs[("/datum/widget", "inspect_widget")].kind == "verb"


def test_scattered_defs_inheritance_and_mtime_refresh(tmp_path: Path) -> None:
    (tmp_path / "code").mkdir()
    f1 = tmp_path / "code" / "a.dm"
    f2 = tmp_path / "code" / "b.dm"
    f1.write_text(
        "/atom/movable/proc/Move()\n\treturn 1\n\n"
        "/mob\n\tvar/health = 100\n\tMove()\n\t\treturn ..()\n",
        encoding="utf-8",
    )
    f2.write_text(
        "/mob/living\n\tname = \"living\"\n\n"
        "/mob/living/custom\n\tparent_type = /mob\n\thealth = 250\n",
        encoding="utf-8",
    )

    idx = DmIndex(tmp_path, repo_name="test")
    assert idx.ensure_fresh() is True
    assert idx.ensure_fresh() is False

    t_custom = idx.get_type("/mob/living/custom", include_inherited=True)
    assert t_custom["parent_type"] == "/mob"
    assert t_custom["inheritance_chain"] == [
        "/mob/living/custom",
        "/mob",
        "/atom/movable",
        "/atom",
        "/datum",
    ]
    vars_map = {v["name"]: v for v in t_custom["vars"]}
    assert vars_map["health"]["value"] == "250"
    assert vars_map["health"]["inherited"] is False

    p_chain = idx.get_proc("/mob/living/custom", "Move", include_parents=True)
    assert p_chain["count"] == 2
    assert [impl["typepath"] for impl in p_chain["implementations"]] == [
        "/mob",
        "/atom/movable",
    ]

    # Modify file with new mtime + size
    time.sleep(0.02)
    f2.write_text(
        "/mob/living\n\tname = \"living\"\n\n"
        "/mob/living/custom\n\tparent_type = /mob\n\thealth = 300\n",
        encoding="utf-8",
    )
    os.utime(f2, None)
    assert idx.ensure_fresh() is True
    t_custom2 = idx.get_type("/mob/living/custom", include_inherited=False)
    vars_map2 = {v["name"]: v for v in t_custom2["vars"]}
    assert vars_map2["health"]["value"] == "300"

    # Delete f2
    f2.unlink()
    assert idx.ensure_fresh() is True
    missing = idx.get_type("/mob/living/custom")
    assert "error" in missing


def test_config_multi_repos_and_path_traversal(tmp_path: Path) -> None:
    r1 = tmp_path / "paradise"
    r2 = tmp_path / "tg"
    r1.mkdir()
    r2.mkdir()
    cfg = load_repo_config(
        {
            "SS13_REPO_PATH": str(r1),
            "SS13_REPOS": json.dumps({"paradise": str(r1), "tg": str(r2)}),
        }
    )
    assert cfg.resolve(None) == ("paradise", r1.resolve())
    assert cfg.resolve("TG") == ("tg", r2.resolve())
    with pytest.raises(ValueError):
        cfg.resolve("unknown_build")
    with pytest.raises(ValueError):
        safe_resolve_under_repo(r1, "../../Windows/System32")


@pytest.mark.skipif(_SS13_REPO is None, reason="Set SS13_REPO_PATH to run SS13 repo smoke tests")
def test_paradise_dm_smoke() -> None:
    assert _SS13_REPO is not None
    idx = DmIndex(_SS13_REPO, repo_name="paradise")
    idx.ensure_fresh()

    # 1. clockwork_mob.dm
    marauder = idx.get_type("/mob/living/simple_animal/hostile/clockwork/marauder")
    assert "error" not in marauder
    m_vars = {v["name"]: v for v in marauder["vars"]}
    assert m_vars["deflect_chance"]["value"] == "30"
    assert m_vars["health"]["value"] == "200"

    m_proc = idx.get_proc(
        "/mob/living/simple_animal/hostile/clockwork/marauder",
        "deflect_projectile",
    )
    assert m_proc["count"] == 1
    assert "deflect_projectile(obj/projectile/P)" in m_proc["implementations"][0]["source"]

    # 2. clockwork_items.dm
    slab = idx.get_type("/obj/item/clockwork/clockslab", include_inherited=False)
    s_vars = {v["name"]: v for v in slab["vars"]}
    assert "plush_colors" in s_vars
    assert "redfox" in (s_vars["plush_colors"]["value"] or "")

    # 3. cogscarab.dm
    cog = idx.get_type("/mob/living/silicon/robot/cogscarab", include_inherited=False)
    c_vars = {v["name"]: v for v in cog["vars"]}
    assert c_vars["wind_up_timer"]["value"] == "CLOCK_MAX_WIND_UP_TIMER"
    assert "pullable_items" in c_vars

    life_chain = idx.get_proc(
        "/mob/living/silicon/robot/cogscarab",
        "Life",
        include_parents=True,
    )
    assert life_chain["count"] >= 2

    # 4. Scattered definitions of /mob/living/silicon/robot
    robot = idx.get_type("/mob/living/silicon/robot", include_inherited=False)
    assert robot["counts"]["definitions"] >= 10

    # 5. #define CLOCK_MAX_WIND_UP_TIMER
    defs = idx.search_defines("CLOCK_MAX_WIND_UP_TIMER", limit=5)
    assert defs["total_matched"] >= 1
    assert defs["matches"][0]["name"] == "CLOCK_MAX_WIND_UP_TIMER"
    assert defs["matches"][0]["value"] == "150"
    assert defs["matches"][0]["file"] == "code/__DEFINES/clockwork.dm"
    assert defs["matches"][0]["line"] == 116

    # 6. dm_search_code with enclosing type/proc
    hits = idx.search_code(
        "CLOCK_MAX_WIND_UP_TIMER",
        subpath="code/game/gamemodes/clockwork",
        limit=10,
    )
    assert hits["returned"] >= 1
    cog_hits = [h for h in hits["matches"] if h["file"].endswith("cogscarab.dm")]
    assert cog_hits
    assert any(
        h["enclosing_type"] == "/mob/living/silicon/robot/cogscarab"
        and h["enclosing_proc"] == "Life"
        for h in cog_hits
    )
