namespace Content.Shared.Gatherable.Components;

public sealed partial class GatheringProjectileComponent
{
    /// <summary>
    /// Chance to gather on a projectile collision, from zero to one.
    /// </summary>
    [DataField]
    public float Chance = 1f;
}
