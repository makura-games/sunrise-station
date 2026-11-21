using Content.Shared._Sunrise.Disease.Analysis;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Ручной анализатор: проверяет предмет или человека на заразу и печатает отчёт на месте.
/// </summary>
/// <remarks>
/// Полевой прибор рядом со стационарным диагностером. Образец ему не нужен, зато нужна бумага,
/// и очков исследования он не даёт: лабораторная работа остаётся за лабораторией.
/// Заправка бумагой и запуск осмотра предсказываются, поэтому запас бумаги синхронизируется.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseScannerComponent : Component
{
    /// <summary>
    /// Сколько листов осталось в приборе.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Paper = 10;

    /// <summary>
    /// Сколько листов влезает.
    /// </summary>
    [DataField]
    public int MaxPaper = 10;

    /// <summary>
    /// Насколько подробен отчёт.
    /// </summary>
    [DataField]
    public DiseaseAnalysisLevel AnalysisLevel = DiseaseAnalysisLevel.Classification;

    /// <summary>
    /// Сколько длится сканирование.
    /// </summary>
    [DataField]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Прототип распечатанного отчёта.
    /// </summary>
    [DataField]
    public EntProtoId ReportPrototype = "SunriseDiagnosisReportPaper";

    /// <summary>
    /// Звук печати.
    /// </summary>
    [DataField]
    public SoundSpecifier? PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
}
