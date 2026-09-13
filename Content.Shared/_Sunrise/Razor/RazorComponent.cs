// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt;

using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Razor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RazorComponent : Component
{
    [DataField]
    public DoAfterId? DoAfter;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan AddSlotTime = TimeSpan.FromSeconds(10);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan RemoveSlotTime = TimeSpan.FromSeconds(8);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan SelectSlotTime = TimeSpan.FromSeconds(6);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? ChangeHairSound = new SoundPathSpecifier("/Audio/Items/scissors.ogg");

}
