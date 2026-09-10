#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-прототипу.
namespace Content.Server.AlertLevel;

public sealed partial class AlertLevelDetail
{
    /// <summary>
    /// Whether this level is an additional protocol that can be active alongside the primary alert level.
    /// </summary>
    [DataField]
    public bool IsAdditional { get; private set; }

    /// <summary>
    /// Determines which active alert level controls single-state visuals such as emergency lights.
    /// </summary>
    [DataField]
    public int VisualPriority { get; private set; }
}
