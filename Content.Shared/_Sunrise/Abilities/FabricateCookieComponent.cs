using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Abilities;

[RegisterComponent]
public sealed partial class FabricateCookieComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("cookieList")]
    public List<string> CookieList = new()
    {
        "FoodBakedCookieOatmeal"
    };

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId ActionFabricateCookie = "FabricateCookie";
}


public sealed partial class FabricateCookieActionEvent : InstantActionEvent {}
