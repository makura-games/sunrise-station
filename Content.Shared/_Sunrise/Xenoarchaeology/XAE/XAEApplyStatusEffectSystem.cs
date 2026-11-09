using Content.Shared._Sunrise.Xenoarchaeology.XAE.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;

namespace Content.Shared._Sunrise.Xenoarchaeology.XAE;

public sealed partial class XAEApplyStatusEffectSystem : BaseXAESystem<XAEApplyStatusEffectComponent>
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    protected override void OnActivated(Entity<XAEApplyStatusEffectComponent> ent,
        ref XenoArtifactNodeActivatedEvent args)
    {
        _statusEffects.TrySetStatusEffectDuration(args.Artifact, ent.Comp.StatusEffect);
    }
}
