using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Прогрессия вампира: вся выпитая кровь, уровни силы, пороги.
/// Отвечает за TotalBlood, DrunkBlood, FullPower и связанные с ними данные.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class VampireProgressionComponent : Component
{
    /// <summary>
    /// Общий объём выпитой крови за всё время. Используется для открытия способностей.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TotalBlood = 0;

    /// <summary>
    /// Общий объём выпитой крови вампира, используется для расчёта стоимости.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int DrunkBlood = 0;

    /// <summary>
    /// укусы с момента последнего ослепления.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BlindInc = 0;

    /// <summary>
    /// Общий объём крови, необходимый для предложения выбора класса.
    /// </summary>
    [DataField]
    public int ClassSelectThreshold = 150;

    /// <summary>
    /// Достигнута ли полная сила вампира.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), DataField, AutoNetworkedField]
    public bool FullPower = false;

    /// <summary>Количество выпитой крови для максимального уровня</summary>
    [DataField]
    public int FullPowerThreshold = 1000;

    /// <summary>Количество уникальных жертв для достижения максимального уровня</summary>
    [DataField]
    public int FullPowerUniqueHumanoids = 8;

    /// <summary>Количество выпитой крови для уровня «средний»</summary>
    [DataField]
    public int MidPowerThreshold = 200;

    /// <summary>Количество выпитой крови для уровня «высокий»</summary>
    [DataField]
    public int HighPowerThreshold = 600;

    /// <summary>Количество уникальных жертв, из которых вампир пил</summary>
    [ViewVariables(VVAccess.ReadOnly), DataField, AutoNetworkedField]
    public int UniqueHumanoidVictims = 0;

    /// <summary>
    /// Следующее время планового обновления вампира (тик систем).
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// Время последнего обновления вампира.
    /// </summary>
    [AutoPausedField]
    public TimeSpan LastUpdate;

    /// <summary>
    /// Интервал планового обновления вампира.
    /// </summary>
    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);
}
