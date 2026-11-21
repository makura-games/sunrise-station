using Content.Server._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Components;

/// <summary>
/// Лабораторное дело по штамму: что о нём узнали анализы и как идёт подбор вакцины.
/// </summary>
/// <remarks>
/// Висит на сущности самого штамма, а не в словаре системы: живёт и умирает вместе с ним, поэтому
/// не нуждается в уборке на рестарте раунда и виден в ViewVariables рядом со свойствами болезни.
/// Появление компонента и означает, что дело открыто: до первого анализа станция о штамме
/// не знает ничего, кроме кода.
/// </remarks>
[RegisterComponent]
[Access(typeof(DiseaseResearchSystem))]
public sealed partial class DiseaseResearchCaseComponent : Component
{
    /// <summary>
    /// Пробные партии вакцины. Пусто, пока данных анализов не хватает на их проверку.
    /// </summary>
    [ViewVariables]
    public readonly List<DiseaseCandidate> Candidates = [];

    /// <summary>
    /// Индекс подходящей партии или -1, пока партии не собраны.
    /// </summary>
    [ViewVariables]
    public int SolutionIndex = -1;

    /// <summary>
    /// Симптомы, замеченные анализами у пациентов или выявленные в культурах.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<ProtoId<DiseaseSymptomPrototype>> ObservedSymptoms = [];

    /// <summary>
    /// Выявил ли анализ класс возбудителя.
    /// </summary>
    [ViewVariables]
    public bool ClassificationKnown;

    /// <summary>
    /// Накопленные очки исследования.
    /// </summary>
    [ViewVariables]
    public int Research;

    /// <summary>
    /// Найден ли рецепт: подходящая партия уже синтезирована хотя бы раз.
    /// </summary>
    [ViewVariables]
    public bool Solved;
}

/// <summary>
/// Пробная партия вакцины внутри дела.
/// </summary>
public sealed class DiseaseCandidate(string code, List<DiseaseClaimSlot> slots)
{
    /// <summary>
    /// Код партии.
    /// </summary>
    public readonly string Code = code;

    /// <summary>
    /// Слоты заявленных свойств.
    /// </summary>
    public readonly List<DiseaseClaimSlot> Slots = slots;

    /// <summary>
    /// Отбракована ли партия неудачным синтезом.
    /// </summary>
    public bool RuledOut;
}

/// <summary>
/// Слот заявленного свойства партии.
/// </summary>
public sealed class DiseaseClaimSlot(int revealAt)
{
    /// <summary>
    /// Свойство в слоте. Null, пока слот не расшифрован или подходящего свойства не нашлось.
    /// </summary>
    public DiseaseClaim? Claim;

    /// <summary>
    /// Прогресс исследования, при котором слот расшифровывается. Ноль — виден сразу.
    /// </summary>
    public readonly int RevealAt = revealAt;

    /// <summary>
    /// Обязан ли слот содержать неверное свойство.
    /// </summary>
    public bool Wrong;
}
