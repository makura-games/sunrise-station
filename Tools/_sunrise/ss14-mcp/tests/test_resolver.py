"""Unit and smoke tests for YAML loading, ProtoIndex, and PrototypeResolver."""

from __future__ import annotations

import os
from pathlib import Path

from ss14_mcp.config import find_repo_root
from ss14_mcp.proto_index import ProtoIndex
from ss14_mcp.resolver import PrototypeResolver
from ss14_mcp.yaml_loader import USING_C_LOADER, load_yaml_entries_with_lines


def _make_mini_repo(tmp_path: Path) -> Path:
    (tmp_path / "Resources" / "Prototypes" / "_Sunrise" / "Partial").mkdir(parents=True)
    (tmp_path / "Content.Server").mkdir(parents=True)
    return tmp_path


def test_c_loader_active() -> None:
    assert USING_C_LOADER is True


def test_yaml_custom_tags_and_lines() -> None:
    yml = """
- type: entity
  id: TestObj
  components:
  - type: Damageable
    damageModifierSet: !type:DamageModifierSetPrototype
      coefficients:
        Blunt: 0.5
  - type: AmbientSound
    sound: !type:SoundPathSpecifier
      path: /Audio/beep.ogg
"""
    entries = load_yaml_entries_with_lines(yml)
    assert len(entries) == 1
    line, data = entries[0]
    assert line == 2
    assert data["id"] == "TestObj"
    assert data["components"][0]["damageModifierSet"]["__tag__"] == "DamageModifierSetPrototype"
    assert data["components"][1]["sound"]["__tag__"] == "SoundPathSpecifier"


def test_inheritance_component_merge_partial_and_list_warning(tmp_path: Path) -> None:
    repo = _make_mini_repo(tmp_path)
    base_yml = """
- type: entity
  id: BaseA
  abstract: true
  components:
  - type: Tag
    tags:
    - Metal
    - NoPaint
  - type: Sprite
    sprite: base.rsi
    state: icon

- type: entity
  id: BaseB
  abstract: true
  components:
  - type: Contraband
    severity: Minor

- type: entity
  id: ChildItem
  parent: [BaseA, BaseB]
  components:
  - type: Tag
    tags:
    - CustomOnly
  - type: Sprite
    state: child_icon
"""
    (repo / "Resources" / "Prototypes" / "test.yml").write_text(base_yml, encoding="utf-8")

    partial_yml = """
- type: !PartialOnly entity
  id: BaseB
  components:
  - type: Contraband
    severity: Major
"""
    (repo / "Resources" / "Prototypes" / "_Sunrise" / "Partial" / "patch.yml").write_text(
        partial_yml, encoding="utf-8"
    )

    idx = ProtoIndex(repo)
    resolver = PrototypeResolver(idx)

    res = resolver.resolve_prototype("ChildItem")
    assert "error" not in res
    assert res["id"] == "ChildItem"
    assert len(res["parent_chain"]) == 2
    assert len(res["partials_applied"]) == 1

    comps = {c["type"]: c for c in res["components"]}
    assert comps["Sprite"]["sprite"] == "base.rsi"
    assert comps["Sprite"]["state"] == "child_icon"
    assert comps["Contraband"]["severity"] == "Major"
    assert comps["Tag"]["tags"] == ["CustomOnly"]
    assert any("Tag.tags overrides parent values" in w for w in res["warnings"])

    # Query excludes abstract by default, finds inherited Contraband
    q = resolver.query_prototypes(component="Contraband")
    assert q["total_matched"] == 1
    assert q["items"][0]["id"] == "ChildItem"

    # Query by parent
    q_p = resolver.query_prototypes(parent="BaseA")
    assert q_p["total_matched"] == 1

    # Tree up and down
    tree = resolver.get_inheritance_tree("BaseA", direction="down", max_depth=3)
    assert tree["descendants"][0]["id"] == "ChildItem"


def test_partial_clear_remove_and_index(tmp_path: Path) -> None:
    repo = _make_mini_repo(tmp_path)
    (repo / "Resources" / "Prototypes" / "helmets.yml").write_text(
        """
- type: entity
  id: TestHelmet
  components:
  - type: Sprite
    sprite: old.rsi
    layers:
    - state: old1
    - state: old2
  - type: Tag
    tags:
    - KeepMe
    - DropMe
""",
        encoding="utf-8",
    )
    (repo / "Resources" / "Prototypes" / "_Sunrise" / "Partial" / "helmets.yml").write_text(
        """
- type: !PartialOnly entity
  id: TestHelmet
  components:
  - type: Sprite
    sprite: new.rsi
    layers: !Clear
    - !Index:0
      state: first
    - !Index:0
      state: zero
  - type: Tag
    tags:
    - !Remove DropMe
    - AddedTag
""",
        encoding="utf-8",
    )

    resolver = PrototypeResolver(ProtoIndex(repo))
    res = resolver.resolve_prototype("TestHelmet")
    comps = {c["type"]: c for c in res["components"]}
    assert comps["Sprite"]["sprite"] == "new.rsi"
    assert [x["state"] for x in comps["Sprite"]["layers"]] == ["zero", "first"]
    assert comps["Tag"]["tags"] == ["KeepMe", "AddedTag"]


def test_incremental_mtime_refresh(tmp_path: Path) -> None:
    repo = _make_mini_repo(tmp_path)
    pfile = repo / "Resources" / "Prototypes" / "items.yml"
    pfile.write_text("- type: entity\n  id: ItemOne\n  name: OldName\n", encoding="utf-8")

    idx = ProtoIndex(repo)
    resolver = PrototypeResolver(idx)
    assert resolver.resolve_prototype("ItemOne")["fields"]["name"] == "OldName"
    assert idx.ensure_fresh() is False

    # Update file and bump mtime
    pfile.write_text("- type: entity\n  id: ItemOne\n  name: NewName\n", encoding="utf-8")
    st = pfile.stat()
    os.utime(pfile, ns=(st.st_atime_ns, st.st_mtime_ns + 1_000_000_000))

    assert resolver.resolve_prototype("ItemOne")["fields"]["name"] == "NewName"


def test_missing_prototype_error(tmp_path: Path) -> None:
    repo = _make_mini_repo(tmp_path)
    (repo / "Resources" / "Prototypes" / "a.yml").write_text(
        "- type: entity\n  id: SimilarItemAlpha\n", encoding="utf-8"
    )
    resolver = PrototypeResolver(ProtoIndex(repo))
    res = resolver.resolve_prototype("SimilarItemMissing")
    assert "error" in res
    assert res["proto_id"] == "SimilarItemMissing"


def test_yaml_12_booleans_keep_off_on_yes_no_as_strings() -> None:
    yml = """
- type: entity
  id: HelmetBoolTest
  components:
  - type: Clothing
    equippedPrefix: off
    altPrefix: on
    yesVal: yes
    noVal: no
    quickEquip: false
    enabled: true
"""
    entries = load_yaml_entries_with_lines(yml)
    comp = entries[0][1]["components"][0]
    assert comp["equippedPrefix"] == "off"
    assert isinstance(comp["equippedPrefix"], str)
    assert comp["altPrefix"] == "on"
    assert comp["yesVal"] == "yes"
    assert comp["noVal"] == "no"
    assert comp["quickEquip"] is False
    assert comp["enabled"] is True


def test_real_repo_smoke_prototypes() -> None:
    repo = find_repo_root()
    idx = ProtoIndex(repo)
    resolver = PrototypeResolver(idx)

    # SheetBrass has a _Sunrise/Partial patch adding Tag SunriseClockworkBrassSheet
    brass = resolver.resolve_prototype("SheetBrass")
    assert "error" not in brass
    assert any("_Sunrise/Partial" in p for p in brass["partials_applied"])
    comps = {c["type"]: c for c in brass["components"]}
    assert "RadialBuilderMaterial" in comps
    assert "SunriseClockworkBrassSheet" in comps["Tag"]["tags"]
    assert any(
        "Tag.tags overrides parent values" in w and "partial" in w.lower()
        for w in brass["warnings"]
    )

    # BaseBorgChassisNT has multiple inheritance [BaseBorgChassis, BaseBorgTransponder]
    borg = resolver.resolve_prototype("BaseBorgChassisNT")
    assert "error" not in borg
    assert len(borg["parents"]) == 2

    # ClothingHeadHelmetHardsuitCybersun uses !Clear + !Index:0 in partial and equippedPrefix: off
    cybersun = resolver.resolve_prototype(
        "ClothingHeadHelmetHardsuitCybersun", components=["Sprite", "Clothing"]
    )
    assert "error" not in cybersun
    cyb_comps = {c["type"]: c for c in cybersun["components"]}
    assert cyb_comps["Sprite"]["sprite"] == "_Sunrise/Clothing/Head/Hardsuits/cybersun.rsi"
    assert cyb_comps["Clothing"]["equippedPrefix"] == "off"
    # Tag is automatically included when components=[...] is provided
    assert "Tag" in cyb_comps

    # AirlockServiceLocked uses layers: !Remove in partial
    airlock = resolver.resolve_prototype("AirlockServiceLocked")
    assert "error" not in airlock
    air_comps = {c["type"]: c for c in airlock["components"]}
    assert "layers" not in air_comps["Sprite"]

    # Query by Sunrise tag
    q = resolver.query_prototypes(tag="SunriseClockworkBrassSheet", limit=10)
    assert q["total_matched"] >= 1
    assert any(item["id"] == "SheetBrass" for item in q["items"])


def test_find_repo_root_cwd_and_outside_fallback(tmp_path: Path, monkeypatch) -> None:
    expected = find_repo_root()
    mini = _make_mini_repo(tmp_path / "other_clone")
    sub = mini / "Resources" / "Prototypes"
    monkeypatch.delenv("SS14_REPO_PATH", raising=False)

    monkeypatch.chdir(sub)
    assert find_repo_root() == mini.resolve()

    outside = tmp_path / "outside"
    outside.mkdir()
    monkeypatch.chdir(outside)
    assert find_repo_root() == expected
