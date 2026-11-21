using Content.Shared._Sunrise.Disease.Analysis;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Машина, которая анализирует вставленный мазок и печатает отчёт.
/// Образец не расходуется: он ещё понадобится вакцинатору.
/// </summary>
/// <remarks>
/// Слот доступен на клиенте для предсказания вставки и извлечения. Сам анализ выполняет сервер.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class DiseaseDiagnoserComponent : Component
{
    /// <summary>
    /// Идентификатор слота для мазка.
    /// </summary>
    [DataField]
    public string SampleSlotId = "diseaseSample";

    /// <summary>
    /// Слот для мазка.
    /// </summary>
    [DataField]
    public ItemSlot SampleSlot = new();

    /// <summary>
    /// Насколько подробный анализ выдаёт машина.
    /// </summary>
    [DataField]
    public DiseaseAnalysisLevel AnalysisLevel = DiseaseAnalysisLevel.Exact;

    /// <summary>
    /// Сколько длится анализ.
    /// </summary>
    [DataField]
    public TimeSpan PrintDelay = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Прототип бумаги с отчётом.
    /// </summary>
    [DataField]
    public EntProtoId ReportPrototype = "SunriseDiagnosisReportPaper";

    /// <summary>
    /// Сколько очков исследования даёт анализ образца с носителя.
    /// Такой образец ещё и показывает, какие симптомы болезнь даёт людям.
    /// </summary>
    [DataField]
    public int HostResearchPoints = 1;

    /// <summary>
    /// Сколько очков исследования даёт анализ выращенной культуры.
    /// Культура не показывает симптомов у людей, зато даёт больше лабораторных данных.
    /// </summary>
    [DataField]
    public int CultureResearchPoints = 2;

    /// <summary>
    /// Звук работы машины во время анализа.
    /// </summary>
    [DataField]
    public SoundSpecifier? RunningSound = new SoundPathSpecifier("/Audio/Machines/diagnoser_printing.ogg");

    /// <summary>
    /// Звук печати готового отчёта.
    /// </summary>
    [DataField]
    public SoundSpecifier? PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
}

/// <summary>
/// Диагностер, который сейчас обрабатывает образец.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveDiseaseDiagnoserComponent : Component
{
    /// <summary>
    /// Когда закончится анализ.
    /// </summary>
    [ViewVariables]
    public TimeSpan FinishAt;
}
