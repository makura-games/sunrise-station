using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Временно показывает игроку учебный алерт без воздействия на здоровье или атмосферу.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialSimulatedAlertComponent : Component
{
    /// <summary>
    /// Прототип алерта, который показывается во время шага.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> Alert = "LowOxygen";

    /// <summary>
    /// Уровень серьёзности показываемого алерта.
    /// </summary>
    [DataField, AutoNetworkedField]
    public short Severity = 2;
}
