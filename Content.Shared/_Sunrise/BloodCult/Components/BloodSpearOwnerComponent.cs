namespace Content.Shared._Sunrise.BloodCult.Components;

[RegisterComponent]
public sealed partial class BloodSpearOwnerComponent : Component
{
    [DataField("maxReturnDistance"), ViewVariables(VVAccess.ReadWrite)]
    public float MaxReturnDistance = 15;

    [DataField("returnSpearActionId"), ViewVariables(VVAccess.ReadWrite)]
    public string ReturnSpearActionId = "ActionCultReturnBloodSpear";

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Spear;
}
