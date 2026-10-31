using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Временно разрешает подбирать только перечисленные предметы в активном шаге туториала.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialPickupSoftLockComponent : Component
{
    /// <summary>
    /// Полностью запрещает подбор предметов на время шага.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BlockAll;

    /// <summary>
    /// Предметы, которые разрешено поднимать в текущем шаге.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> AllowedItems = [];

    /// <summary>
    /// Сообщение при попытке поднять другой предмет.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-pickup-highlighted";
}
