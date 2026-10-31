using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Временно запрещает экипировать перечисленные предметы в указанные слоты или во все слоты.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialEquipBlockedSoftLockComponent : Component
{
    /// <summary>
    /// Предметы, экипировку которых нужно заблокировать.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> Items = [];

    /// <summary>
    /// Слоты, в которые запрещено экипировать предметы.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SlotFlags Slots = SlotFlags.NONE;

    /// <summary>
    /// Запрещает экипировать предметы во все слоты.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BlockAllSlots;

    /// <summary>
    /// Сообщение при попытке экипировать заблокированный предмет.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-use-highlighted-inventory";
}
