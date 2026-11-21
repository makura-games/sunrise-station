using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Стерилизующая арка: обдаёт прошедшего дезинфектантом из своего бака и пищит,
/// если через неё прошёл заражённый.
/// </summary>
/// <remarks>
/// Болезнь внутри живого арка не лечит — это работа вакцины. Она гасит очаги на вещах прошедшего
/// и снимает заразу с мёртвого тела на каталке, поэтому стоит у входа в карантин.
/// Распыление предсказывается на клиенте, а заражённость проверяет только сервер:
/// данные о болезнях клиенту не отдаются.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class DiseaseSterilizerComponent : Component
{
    /// <summary>
    /// Бак с реагентом.
    /// </summary>
    [DataField]
    public string SolutionName = "tank";

    /// <summary>
    /// Реагент, который расходует арка.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "SunriseDisinfectant";

    /// <summary>
    /// Сколько реагента уходит на один проход.
    /// </summary>
    [DataField]
    public FixedPoint2 Cost = FixedPoint2.New(2);

    /// <summary>
    /// Звук распыления.
    /// </summary>
    [DataField]
    public SoundSpecifier? SpraySound = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");

    /// <summary>
    /// Писк, когда через арку прошёл заражённый.
    /// </summary>
    [DataField]
    public SoundSpecifier? AlarmSound = new SoundPathSpecifier("/Audio/_Sunrise/Effects/ScanGate/scan_fail.ogg");

    /// <summary>
    /// Облачко, которое остаётся в арке после распыления.
    /// </summary>
    [DataField]
    public EntProtoId? MistPrototype;

    /// <summary>
    /// Сколько арка показывает распыление или тревогу, прежде чем вернуться в ожидание.
    /// </summary>
    [DataField]
    public TimeSpan StateDuration = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Когда арка вернётся в ожидание.
    /// </summary>
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan StateResetTime;
}

/// <summary>
/// Ключи внешнего вида стерилизующей арки.
/// </summary>
[Serializable, NetSerializable]
public enum DiseaseSterilizerVisuals : byte
{
    /// <summary>
    /// Что показывает арка. Значение — <see cref="DiseaseSterilizerState"/>.
    /// </summary>
    State,
}

/// <summary>
/// Что показывает стерилизующая арка.
/// </summary>
[Serializable, NetSerializable]
public enum DiseaseSterilizerState : byte
{
    /// <summary>
    /// Ожидание прохода.
    /// </summary>
    Idle,

    /// <summary>
    /// Прошедшего обдало дезинфектантом.
    /// </summary>
    Spraying,

    /// <summary>
    /// Через арку прошёл заражённый.
    /// </summary>
    Alarm,
}
