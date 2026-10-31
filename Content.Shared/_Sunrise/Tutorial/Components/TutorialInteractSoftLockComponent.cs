using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Временно блокирует взаимодействие удерживаемым предметом, если он указан, с окружающим миром.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialInteractSoftLockComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<EntProtoId> Items = [];

    [DataField, AutoNetworkedField]
    public List<EntProtoId> Targets = [];

    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-action-blocked";
}
