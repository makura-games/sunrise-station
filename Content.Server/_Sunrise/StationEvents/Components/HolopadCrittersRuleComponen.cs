using Content.Shared.Storage;
using Content.Server.StationEvents.Events;

namespace Content.Server.StationEvents.Components;

/// <summary>
/// Spawns critters from holopads during a station event.
/// </summary>
[RegisterComponent, Access(typeof(HolopadCrittersRule))]
public sealed partial class HolopadCrittersRuleComponent : Component
{
    /// <summary>
    /// List of entities that can spawn from holopads.
    /// </summary>
    [DataField("entries")]
    public List<EntitySpawnEntry> Entries = new();

    /// <summary>
    /// At least one special entry is guaranteed to spawn on a random holopad.
    /// </summary>
    [DataField("specialEntries")]
    public List<EntitySpawnEntry> SpecialEntries = new();

    /// <summary>
    /// Should the event spawn entities from ALL holopads or just one?
    /// </summary>
    [DataField]
    public bool SpawnFromAllHolopads = true;

    /// <summary>
    /// Maximum number of holopads to spawn from (if SpawnFromAllHolopads is false).
    /// </summary>
    [DataField]
    public int MaxHolopadsToSpawn = 1;

    /// <summary>
    /// Should holopads be disabled after spawning?
    /// </summary>
    [DataField]
    public bool DisableHolopadsAfterSpawn = false;
}