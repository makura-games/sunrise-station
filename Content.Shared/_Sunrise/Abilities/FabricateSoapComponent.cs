using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Abilities;

[RegisterComponent]
public sealed partial class FabricateSoapComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("soapList")]
    public List<string> SoapList = new()
    {
        "Soap"
    };

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId ActionFabricateSoap = "FabricateSoap";
}


public sealed partial class FabricateSoapActionEvent : InstantActionEvent {}
