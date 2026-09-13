using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Временно блокирует употребление выбранных сущностей.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialIngestSoftLockComponent : Component, ITutorialEntitySoftLockComponent
{
    /// <summary>
    /// Прототипы сущностей, употребление которых необходимо заблокировать.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> Targets = [];

    /// <summary>
    /// Сообщение при попытке употребить заблокированную сущность.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId Popup = "tutorial-softlock-action-blocked";
}
