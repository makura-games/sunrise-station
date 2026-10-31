using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Временно разрешает игроку туториала только заданные виды атак.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialAttackSoftLockComponent : Component
{
    /// <summary>
    /// Прототипы сущностей, которые разрешено атаковать.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> AllowedTargets = [];

    /// <summary>
    /// Прототипы оружия, которыми разрешены ближние атаки.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> AllowedMeleeWeapons = [];

    /// <summary>
    /// Прототипы оружия, из которого разрешено стрелять.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> AllowedRangedWeapons = [];

    /// <summary>
    /// Разрешены ли удары без оружия.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AllowUnarmed;

    /// <summary>
    /// Разрешено ли действие обезоруживания.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AllowDisarm;

    /// <summary>
    /// Нужно ли блокировать атаку без показа всплывающего сообщения.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Silent;

    /// <summary>
    /// Сообщение при заблокированной атаке.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Popup = "tutorial-softlock-action-blocked";
}
