namespace Content.Server.Gatherable.Components;

public sealed partial class GatheringProjectileComponent
{
    /// <summary>
    /// Chance to gather. 1.0 = 100%.
    /// </summary>
    [DataField]
    public float Chance = 1f;
}
