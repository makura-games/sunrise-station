using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.BloodCult.Juggernaut;

[RegisterComponent]
public sealed partial class JuggernautComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId HummerSpawnId = "HammerJuggernaut";
}
