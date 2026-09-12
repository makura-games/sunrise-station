using Content.Shared.Alert;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Состояние вампира.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class VampireComponent : Component
{
    /// <summary>
    /// Иконка вампира.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<FactionIconPrototype> StatusIcon = "VampireMasterIcon";

    /// <summary>
    /// Счётчик крови.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> BloodAlert = "VampireBlood";

    /// <summary>
    /// Предел отображаемой крови.
    /// </summary>
    public const int MaxDisplayedBlood = 9999;

    /// <summary>
    /// Запас крови.
    /// </summary>
    [AutoNetworkedField]
    public int DrunkBlood;

    /// <summary>
    /// Клыки выпущены.
    /// </summary>
    [AutoNetworkedField]
    public bool FangsExtended;

    /// <summary>
    /// Текущая сытость.
    /// </summary>
    [AutoNetworkedField]
    public float BloodFullness = 90f;

    /// <summary>
    /// Скорость ходьбы при голоде.
    /// </summary>
    [DataField]
    public float StarvationWalkSpeedModifier = 0.7f;

    /// <summary>
    /// Скорость бега при голоде.
    /// </summary>
    [DataField]
    public float StarvationSprintSpeedModifier = 0.7f;

    /// <summary>
    /// Текущий уровень силы.
    /// </summary>
    [DataField, AutoNetworkedField]
    public VampirePowerLevel PowerLevel = VampirePowerLevel.Neonate;

}

/// <summary>
/// Слои счётчика крови.
/// </summary>
public enum VampireVisualLayers : byte
{
    Digit1,
    Digit2,
    Digit3,
    Digit4,
}
