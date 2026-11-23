// ReSharper disable once CheckNamespace
namespace Content.Shared.AbstractAnalyzer;

public abstract partial class AbstractAnalyzerComponent
{
    /// <summary>
    /// Временное состояние непрерывного сканирования до принятия Wizden общего рефакторинга анализаторов.
    /// </summary>
    [DataField]
    public bool IsAnalyzerActive;
}
