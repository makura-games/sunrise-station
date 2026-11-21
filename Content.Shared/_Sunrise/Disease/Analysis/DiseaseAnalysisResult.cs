using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Analysis;

/// <summary>
/// Итог анализа образца. Внутри лежит только то, что разрешает <see cref="Level"/>,
/// поэтому результат можно отдавать в клиентский интерфейс как есть.
/// </summary>
[Serializable, NetSerializable]
public sealed class DiseaseAnalysisResult(DiseaseAnalysisLevel level, string subjectName, List<DiseaseAnalysisEntry> entries)
{
    /// <summary>
    /// Уровень анализа, на котором собран этот итог.
    /// </summary>
    public readonly DiseaseAnalysisLevel Level = level;

    /// <summary>
    /// Название существа или предмета на момент взятия образца.
    /// </summary>
    public readonly string SubjectName = subjectName;

    /// <summary>
    /// По одной записи на каждую найденную болезнь. Пусто, если образец чистый.
    /// </summary>
    public readonly List<DiseaseAnalysisEntry> Entries = entries;
}

/// <summary>
/// Данные анализа по одной найденной болезни. Поля выше уровня анализа остаются null или пустыми.
/// </summary>
[Serializable, NetSerializable]
public sealed class DiseaseAnalysisEntry(
    LocId syndrome,
    List<LocId> symptoms,
    DiseaseClassification? classification,
    string? strainCode,
    int? stage,
    List<LocId> treatmentHints)
{
    /// <summary>
    /// Наблюдаемый синдром. Есть всегда.
    /// </summary>
    public readonly LocId Syndrome = syndrome;

    /// <summary>
    /// Названия симптомов, проявившихся у носителя. Есть всегда; для образцов с предметов пусто.
    /// </summary>
    public readonly List<LocId> Symptoms = symptoms;

    /// <summary>
    /// Класс возбудителя. Появляется начиная с <see cref="DiseaseAnalysisLevel.Classification"/>.
    /// </summary>
    public readonly DiseaseClassification? Classification = classification;

    /// <summary>
    /// Код штамма, уникальный в пределах раунда. Появляется на <see cref="DiseaseAnalysisLevel.Exact"/>.
    /// Совпадение кодов означает одну и ту же болезнь, даже если симптомы у носителей разные.
    /// </summary>
    public readonly string? StrainCode = strainCode;

    /// <summary>
    /// Стадия болезни на момент взятия образца, с нуля. Есть на <see cref="DiseaseAnalysisLevel.Exact"/>
    /// для образцов, взятых с носителя.
    /// </summary>
    public readonly int? Stage = stage;

    /// <summary>
    /// Подсказки по лечению. Заполняются на <see cref="DiseaseAnalysisLevel.Exact"/>.
    /// </summary>
    public readonly List<LocId> TreatmentHints = treatmentHints;
}
