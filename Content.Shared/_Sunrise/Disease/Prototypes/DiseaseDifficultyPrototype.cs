using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Профиль сложности исследования штамма: сколько пробных партий вакцины показывает вакцинатор,
/// сколько у них заявленных свойств и сколько из этих свойств закрыто до расшифровки.
/// </summary>
/// <remarks>
/// Профиль выбирается по силе болезни: берётся запись с наибольшим <see cref="MinStrength"/>,
/// не превышающим силу штамма. Так слабая простуда разгадывается с одного отчёта,
/// а тяжёлый штамм требует лабораторной работы.
/// </remarks>
[Prototype]
public sealed partial class DiseaseDifficultyPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Минимальная сила болезни, с которой применяется профиль.
    /// </summary>
    [DataField]
    public int MinStrength;

    /// <summary>
    /// Сколько пробных партий попадает в дело.
    /// </summary>
    [DataField]
    public int Candidates = 6;

    /// <summary>
    /// Сколько свойств заявляет каждая партия.
    /// </summary>
    [DataField]
    public int Claims = 3;

    /// <summary>
    /// Сколько свойств партии закрыто до расшифровки. Не больше, чем <see cref="Claims"/> минус одно:
    /// хотя бы одно свойство должно быть видно сразу.
    /// </summary>
    [DataField]
    public int LockedClaims;

    /// <summary>
    /// Сколько очков исследования снимает один замок.
    /// </summary>
    [DataField]
    public int ResearchPerUnlock = 3;
}
