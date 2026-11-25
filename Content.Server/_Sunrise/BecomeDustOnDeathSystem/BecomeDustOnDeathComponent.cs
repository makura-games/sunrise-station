using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.BecomeDustOnDeathSystem;

[RegisterComponent]
public sealed partial class BecomeDustOnDeathComponent : Component
{
    [DataField("sprite")]
    public EntProtoId SpawnOnDeathPrototype = "Ectoplasm";
}
