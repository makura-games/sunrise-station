using Content.Server.Destructible.Thresholds;

#pragma warning disable IDE0130
namespace Content.Server.Destructible;

public sealed partial class DestructibleSystem
{
    /// <summary>
    /// Replaces all configured damage thresholds with a single threshold.
    /// </summary>
    public void ReplaceThresholds(Entity<DestructibleComponent> entity, DamageThreshold threshold)
    {
        entity.Comp.Thresholds.Clear();
        entity.Comp.Thresholds.Add(threshold);
    }
}
