using Content.Server._Sunrise.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.StationEvents.Components;

/// <summary>
/// Spawns a single entity at a random tile on a station using TryGetRandomTile.
/// </summary>
[RegisterComponent, Access(typeof(LateJoinSpawnRule))]
public sealed partial class LateJoinSpawnRuleComponent : Component
{
    /// <summary>
    /// The entity to be spawned.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype;
}
