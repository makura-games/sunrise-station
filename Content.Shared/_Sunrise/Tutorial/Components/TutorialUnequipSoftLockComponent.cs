using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
///     Временно блокирует снятие выбранных предметов со слотов инвентаря.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialUnequipSoftLockComponent : Component
{
    [DataField, AutoNetworkedField]
    public SlotFlags Slots = SlotFlags.NONE;

    [DataField, AutoNetworkedField]
    public List<EntProtoId> Items = [];

    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-action-blocked";
}
