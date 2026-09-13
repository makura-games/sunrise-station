#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.AbstractAnalyzer;

public abstract partial class AbstractAnalyzerComponent
{
    /// <summary>
    /// Временное состояние непрерывного сканирования до принятия Wizden общего рефакторинга анализаторов.
    /// </summary>
    [DataField]
    public bool IsAnalyzerActive;
}
