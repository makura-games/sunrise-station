using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Analysis;

/// <summary>
/// Строка заявленного свойства партии.
/// </summary>
/// <remarks>
/// Часть свойств закрыта до расшифровки: у таких строк <see cref="Text"/> пуст,
/// а <see cref="RevealAt"/> говорит, при каком прогрессе исследования свойство откроется.
/// </remarks>
[Serializable, NetSerializable]
public sealed class DiseaseClaimLine(string? text, int revealAt)
{
    /// <summary>
    /// Идентификатор локали со свойством. Null, если свойство ещё не расшифровано.
    /// </summary>
    public readonly string? Text = text;

    /// <summary>
    /// Прогресс исследования, при котором свойство расшифровывается. Ноль — видно сразу.
    /// </summary>
    public readonly int RevealAt = revealAt;
}

/// <summary>
/// Пробная вакцина в интерфейсе вакцинатора: код партии и заявленные свойства.
/// Подходит только та, у которой все свойства верны для штамма.
/// </summary>
[Serializable, NetSerializable]
public sealed class DiseaseVaccineCandidate(string code, List<DiseaseClaimLine> lines, bool ruledOut)
{
    /// <summary>
    /// Код партии, по которому партия адресуется в интерфейсе.
    /// </summary>
    public readonly string Code = code;

    /// <summary>
    /// Заявленные свойства партии, включая нерасшифрованные.
    /// </summary>
    public readonly List<DiseaseClaimLine> Lines = lines;

    /// <summary>
    /// Отбракована ли партия неудачным синтезом.
    /// </summary>
    public readonly bool RuledOut = ruledOut;
}

/// <summary>
/// Данные по исследуемому штамму: код штамма, прогресс исследования и партии, которые можно попробовать.
/// Выводы анализов сюда не попадают: их игрок читает по распечаткам диагностера и в базе данных вирусов.
/// </summary>
/// <remarks>
/// Серверная сводка: машина раскладывает её по сетевым полям своего компонента, по сети не ходит.
/// </remarks>
public sealed class DiseaseResearchState(
    string strainCode,
    List<DiseaseVaccineCandidate> candidates,
    string? solvedCode,
    int research,
    int researchGoal,
    bool hasData,
    int missingFacts)
{
    /// <summary>
    /// Код штамма, по которому врач связывает образцы разных пациентов.
    /// </summary>
    public readonly string StrainCode = strainCode;

    /// <summary>
    /// Пробные партии вакцины. Пусто, пока данных анализов не хватает на их проверку.
    /// </summary>
    public readonly List<DiseaseVaccineCandidate> Candidates = candidates;

    /// <summary>
    /// Код подошедшей партии, если рецепт уже найден. Дальше дозы штампуются по нему.
    /// </summary>
    public readonly string? SolvedCode = solvedCode;

    /// <summary>
    /// Накопленные очки исследования штамма.
    /// </summary>
    public readonly int Research = research;

    /// <summary>
    /// Сколько очков нужно, чтобы расшифровать все свойства партий.
    /// </summary>
    public readonly int ResearchGoal = researchGoal;

    /// <summary>
    /// Открыто ли по штамму дело: был ли хоть один отчёт диагностера.
    /// </summary>
    public readonly bool HasData = hasData;

    /// <summary>
    /// Скольких подтверждённых фактов не хватает, чтобы собрать партии.
    /// </summary>
    /// <remarks>
    /// Факт — это выявленный класс возбудителя или замеченный у пациентов симптом.
    /// Ноль означает, что партии уже собраны.
    /// </remarks>
    public readonly int MissingFacts = missingFacts;
}
