using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Abilities;

[RegisterComponent]
public sealed partial class BorgCuffedComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("cableCuffs")]
    public EntProtoId CableCuffsId = "Cablecuffs";

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId CuffActionId = "BorgCuffed";

    [ViewVariables(VVAccess.ReadWrite),
     DataField("cuffTime")]
    public float CuffTime = 3.5f;
}


public sealed partial class BorgCuffedActionEvent : EntityTargetActionEvent
{

}

[Serializable, NetSerializable]
public sealed partial class BorgCuffedDoAfterEvent : SimpleDoAfterEvent
{

}
