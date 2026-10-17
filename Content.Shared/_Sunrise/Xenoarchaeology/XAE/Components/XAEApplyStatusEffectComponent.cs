using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Xenoarchaeology.XAE.Components;

[RegisterComponent, Access(typeof(XAEApplyStatusEffectSystem))]
public sealed partial class XAEApplyStatusEffectComponent : Component
{
    [DataField(required: true)]
    public EntProtoId<StatusEffectComponent> StatusEffect;
}
