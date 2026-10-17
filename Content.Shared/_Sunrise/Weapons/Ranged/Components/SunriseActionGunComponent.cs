using Content.Shared._Sunrise.Weapons.Ranged.Systems;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Weapons.Ranged.Components;

/// <summary>
/// Grants an action that fires an internal gun entity.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SunriseActionGunSystem))]
public sealed partial class SunriseActionGunComponent : Component
{
    /// <summary>
    /// Action to grant. It must use <see cref="SunriseActionGunShootEvent"/>.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Action = string.Empty;

    public EntityUid? ActionEntity;

    /// <summary>
    /// Gun prototype spawned and owned by this component.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId GunProto = string.Empty;

    public EntityUid? Gun;
}

/// <summary>
/// Fires the internal gun towards the selected world position.
/// </summary>
public sealed partial class SunriseActionGunShootEvent : WorldTargetActionEvent;
