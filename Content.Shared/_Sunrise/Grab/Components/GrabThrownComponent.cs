using Content.Shared.Damage;
using Content.Shared._Sunrise.Grab.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Grab.Components;

/// <summary>
/// Temporary marker for an entity that was thrown from a grab and should hurt itself or the target on collision.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedGrabSystem))]
public sealed partial class GrabThrownComponent : Component
{
    /// <summary>
    /// Damage dealt to the thrown entity when it collides with something hard.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier? DamageOnCollide;

    /// <summary>
    /// Damage dealt to the entity hit by the thrown grabbed entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier? WallDamageOnCollide;

    /// <summary>
    /// Stamina damage dealt to the thrown entity when it collides.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StaminaDamageOnCollide;

    /// <summary>
    /// Prevents the same throw from applying collision effects multiple times before deferred removal lands.
    /// </summary>
    [AutoNetworkedField]
    public bool HasCollided;
}
