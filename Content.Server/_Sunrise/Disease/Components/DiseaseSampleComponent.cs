using Content.Server._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Components;

/// <summary>
/// Образец, снятый мазком или выросший в чашке Петри. Сам факт его наличия помечает носитель использованным.
/// </summary>
[RegisterComponent]
[Access(typeof(DiseaseAnalysisSystem), typeof(DiseaseCultureSystem), typeof(DiseaseDiagnoserSystem))]
public sealed partial class DiseaseSampleComponent : Component
{
    /// <summary>
    /// Болезни, найденные в момент взятия. Пусто — образец отрицательный.
    /// </summary>
    [ViewVariables]
    public List<DiseaseSampleEntry> Entries = new();

    /// <summary>
    /// Имя моба или предмета в момент взятия образца.
    /// </summary>
    [ViewVariables]
    public string SubjectName = string.Empty;

    /// <summary>
    /// Откуда взят образец. От этого зависит, сколько очков исследования даст его анализ.
    /// </summary>
    [ViewVariables]
    public DiseaseSampleKind Kind = DiseaseSampleKind.Host;

    /// <summary>
    /// Выплатил ли этот образец очки исследования.
    /// </summary>
    /// <remarks>
    /// Диагностер образцы не расходует, поэтому без этой отметки один мазок можно было бы
    /// печатать бесконечно и получать очки даром.
    /// </remarks>
    [ViewVariables]
    public bool Counted;
}

/// <summary>
/// Откуда взят образец.
/// </summary>
public enum DiseaseSampleKind : byte
{
    /// <summary>
    /// Мазок с носителя или с заражённого предмета.
    /// </summary>
    Host,

    /// <summary>
    /// Культура, выросшая в чашке Петри.
    /// </summary>
    Culture,
}

/// <summary>
/// Одна болезнь, найденная в образце.
/// </summary>
/// <param name="Disease">Обнаруженная болезнь.</param>
/// <param name="Stage">Стадия носителя, считая с нуля. Null для образцов с заражённых предметов.</param>
/// <param name="Symptoms">Симптомы, проявившиеся у носителя. Пусто для образцов с заражённых предметов.</param>
public readonly record struct DiseaseSampleEntry(
    EntityUid Disease,
    int? Stage,
    List<ProtoId<DiseaseSymptomPrototype>> Symptoms);
