using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Временно блокирует взаимодействие c указанным UI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialOpenUiSoftLockComponent : Component, ITutorialEntitySoftLockComponent
{
    [DataField, AutoNetworkedField]
    public List<EntProtoId> Targets = [];

    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-action-blocked";
}
