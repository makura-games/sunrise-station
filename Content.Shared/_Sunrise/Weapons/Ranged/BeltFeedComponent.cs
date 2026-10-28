using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Weapons.Ranged;

/// <summary>
/// Marks a weapon whose feed cover controls access to its ammunition box.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BeltFeedSystem))]
public sealed partial class BeltFeedComponent : Component
{
    /// <summary>
    /// Whether the weapon must be cycled before it can fire again.
    /// </summary>
    [AutoNetworkedField]
    public bool RequiresCycle;
}
