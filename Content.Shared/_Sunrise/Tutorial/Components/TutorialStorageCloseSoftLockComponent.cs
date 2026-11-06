using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Временно запрещает закрывать выбранные хранилища во время шага туториала.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialStorageCloseSoftLockComponent : Component, ITutorialEntitySoftLockComponent
{
    /// <summary>
    /// Хранилища, которые нельзя закрывать в текущем шаге.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> Targets = [];

    /// <summary>
    /// Сообщение при попытке закрыть хранилище.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-storage-close";
}
