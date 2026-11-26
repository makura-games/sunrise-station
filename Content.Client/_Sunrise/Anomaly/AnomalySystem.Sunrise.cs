using Content.Shared.Anomaly.Components;

#pragma warning disable IDE0130
namespace Content.Client.Anomaly;

public sealed partial class AnomalySystem
{
    private void OnFloatingShutdown(Entity<AnomalyComponent> ent, ref ComponentShutdown args)
    {
        _floating.FloatAnimation(ent, ent.Comp.FloatingOffset, ent.Comp.AnimationKey, ent.Comp.AnimationTime, stop: true);
    }
}
