using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
///     Временно блокирует вставку удерживаемых предметов в выбранные хранилища.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialStorageInsertSoftLockComponent : Component, ITutorialEntitySoftLockComponent
{
    [DataField, AutoNetworkedField]
    public List<EntProtoId> Items = [];

    [DataField, AutoNetworkedField]
    public List<EntProtoId> Targets = [];

    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-action-blocked";
}
