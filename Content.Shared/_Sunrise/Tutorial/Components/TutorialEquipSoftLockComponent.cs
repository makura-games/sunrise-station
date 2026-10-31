using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Временно разрешает экипировать конкретный предмет только в заданный слот.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialEquipSoftLockComponent : Component
{
    /// <summary>
    /// Предмет, который разрешено экипировать в текущем шаге.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId Item;

    /// <summary>
    /// Слот, в который разрешена экипировка предмета.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public SlotFlags Slot = SlotFlags.NONE;

    /// <summary>
    /// Сообщение при попытке использовать другой слот или предмет.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-use-highlighted-inventory";
}
