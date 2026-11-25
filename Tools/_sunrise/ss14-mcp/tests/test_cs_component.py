"""Unit and smoke tests for C# ComponentIndex."""

from __future__ import annotations

import os
from pathlib import Path

from ss14_mcp.config import find_repo_root
from ss14_mcp.cs_component import ComponentIndex


def _make_mini_repo(tmp_path: Path) -> Path:
    (tmp_path / "Resources" / "Prototypes").mkdir(parents=True)
    (tmp_path / "Content.Server").mkdir(parents=True)
    (tmp_path / "Content.Shared").mkdir(parents=True)
    return tmp_path


def test_parse_component_fields_and_partials(tmp_path: Path) -> None:
    repo = _make_mini_repo(tmp_path)
    cs1 = repo / "Content.Shared" / "SampleComponent.cs"
    cs1.write_text(
        """
namespace Content.Shared.Test;

// Commented [DataField] public int Ignored = 1;
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[AutoGenerateComponentPause]
public sealed partial class SampleComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public float Speed = 1.5f;

    [DataField("customTag"), ViewVariables(VVAccess.ReadWrite)]
    public string Label { get; set; } = "abc";

    [DataField, AutoPausedField]
    public TimeSpan NextTick;
}
""",
        encoding="utf-8",
    )
    cs2 = repo / "Content.Server" / "SampleComponent.Server.cs"
    cs2.write_text(
        """
namespace Content.Server.Test;

public sealed partial class SampleComponent
{
    [DataField]
    public bool ServerOnly = true;
}
""",
        encoding="utf-8",
    )

    idx = ComponentIndex(repo)
    res = idx.inspect_component("Sample")
    assert "error" not in res
    assert res["component"] == "SampleComponent"
    assert res["yaml_type"] == "Sample"
    assert res["networked"] is True
    assert res["auto_generate_component_state"] is True
    assert res["auto_generate_component_pause"] is True
    assert len(res["files"]) == 2
    assert res["data_fields_count"] == 4

    by_yaml = {f["yaml_name"]: f for f in res["data_fields"]}
    assert by_yaml["speed"]["required"] is True
    assert by_yaml["speed"]["auto_networked"] is True
    assert by_yaml["speed"]["default"] == "1.5f"
    assert by_yaml["customTag"]["cs_name"] == "Label"
    assert by_yaml["nextTick"]["auto_paused"] is True
    assert by_yaml["serverOnly"]["default"] == "true"

    # Incremental mtime update
    assert idx.ensure_fresh() is False
    cs2.write_text(
        """
public sealed partial class SampleComponent
{
    [DataField]
    public bool ServerOnly = false;
}
""",
        encoding="utf-8",
    )
    st = cs2.stat()
    os.utime(cs2, ns=(st.st_atime_ns, st.st_mtime_ns + 1_000_000_000))
    res2 = idx.inspect_component("SampleComponent")
    by_yaml2 = {f["yaml_name"]: f for f in res2["data_fields"]}
    assert by_yaml2["serverOnly"]["default"] == "false"


def test_missing_component_returns_error(tmp_path: Path) -> None:
    repo = _make_mini_repo(tmp_path)
    idx = ComponentIndex(repo)
    res = idx.inspect_component("NonExistentMagic")
    assert "error" in res


def test_real_repo_smoke_components() -> None:
    repo = find_repo_root()
    idx = ComponentIndex(repo)

    magboots = idx.inspect_component("BorgMagboots")
    assert "error" not in magboots
    assert magboots["networked"] is True
    assert magboots["auto_generate_component_state"] is True
    names = {f["yaml_name"] for f in magboots["data_fields"]}
    assert {"toggleAction", "walkModifier", "sprintModifier", "requiresGrid"}.issubset(names)

    candy = idx.inspect_component("FabricateCandyComponent")
    assert "error" not in candy
    assert candy["data_fields_count"] >= 4


def test_regression_cogscarab_and_borg_chassis_components() -> None:
    repo = find_repo_root()
    idx = ComponentIndex(repo)

    cog = idx.inspect_component("CogscarabComponent")
    assert "error" not in cog
    assert cog["data_fields_count"] == 10
    assert cog["auto_generate_component_state"] is True
    assert cog.get("auto_generate_component_state_args") == "fieldDeltas: true"
    cog_names = {f["cs_name"] for f in cog["data_fields"]}
    assert "ZeroWindUpDamage" in cog_names
    zero_dmg = next(f for f in cog["data_fields"] if f["cs_name"] == "ZeroWindUpDamage")
    assert "Blunt" in (zero_dmg["default"] or "")

    borg = idx.inspect_component("BorgChassisComponent")
    assert "error" not in borg
    assert borg["data_fields_count"] == 15
    borg_names = {f["cs_name"] for f in borg["data_fields"]}
    assert {
        "BrainWhitelist",
        "ModuleWhitelist",
        "SelectedModule",
        "HasMindState",
        "BatteryAlert",
    }.issubset(borg_names)
