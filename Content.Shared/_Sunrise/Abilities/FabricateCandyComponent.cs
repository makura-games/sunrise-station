using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Abilities;

[RegisterComponent]
public sealed partial class FabricateCandyComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId FoodGumballId = "FoodGumball";

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId FoodLollipopId = "FoodLollipop";

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId ActionFabricateLollipop = "FabricateLollipop";

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId ActionFabricateGumball = "FabricateGumball";
}



public sealed partial class FabricateLollipopActionEvent : InstantActionEvent {}

public sealed partial class FabricateGumballActionEvent : InstantActionEvent {}
