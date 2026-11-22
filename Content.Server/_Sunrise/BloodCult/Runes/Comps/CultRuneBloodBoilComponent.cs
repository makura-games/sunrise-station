using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.BloodCult.Runes.Comps;

[RegisterComponent]
public sealed partial class CultRuneBloodBoilComponent : Component
{
    [DataField("maxProjectiles"), ViewVariables(VVAccess.ReadWrite)]
    public int MaxProjectiles = 10;

    [DataField("minProjectiles"), ViewVariables(VVAccess.ReadWrite)]
    public int MinProjectiles = 5;

    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId ProjectilePrototype;

    [DataField("projectileRange"), ViewVariables(VVAccess.ReadWrite)]
    public float ProjectileRange = 50f;

    [DataField("projectileSpeed"), ViewVariables(VVAccess.ReadWrite)]
    public float ProjectileSpeed = 20f;

    [ViewVariables(VVAccess.ReadWrite), DataField("summonMinCount")]
    public uint SummonMinCount = 3;
}
