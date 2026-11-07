using Content.Shared.Projectiles;
using Robust.Shared.GameStates;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Projectiles;

public sealed partial class ProjectileComponent
{
    [DataField]
    [AutoNetworkedField]
    public float ArmorPenetration = 0f;
}
