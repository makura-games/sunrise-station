using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem
{
    // Sunrise-Start
    /// <summary>
    /// Checks if an entity has Mangleness damage greater than zero.
    /// </summary>
    /// <param name="uid">The entity to check</param>
    /// <returns>True if the entity has Mangleness damage greater than zero</returns>
    public bool HasMangleness(EntityUid uid)
    {
        if (!_damageableQuery.TryGetComponent(uid, out var damageable))
            return false;

        return damageable.Damage.DamageDict.GetValueOrDefault("Mangleness", FixedPoint2.Zero) > FixedPoint2.Zero;
    }
    // Sunrise-End
}
