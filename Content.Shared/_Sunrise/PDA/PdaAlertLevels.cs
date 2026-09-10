using Robust.Shared.Serialization;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-контракту.
namespace Content.Shared.PDA;

public sealed partial class PdaComponent
{
    /// <summary>
    /// Primary and additional alert levels currently displayed by this PDA.
    /// </summary>
    [ViewVariables]
    public List<PdaAlertLevelInfo> StationAlertLevels = [];
}

/// <summary>
/// Describes one active station alert level and its display color.
/// </summary>
[Serializable, NetSerializable]
public sealed class PdaAlertLevelInfo
{
    /// <summary>
    /// Prototype identifier of the alert level.
    /// </summary>
    public readonly string Level;

    /// <summary>
    /// Display color of the alert level.
    /// </summary>
    public readonly Color Color;

    /// <summary>
    /// Creates an alert-level display entry for a PDA.
    /// </summary>
    public PdaAlertLevelInfo(string level, Color color)
    {
        Level = level;
        Color = color;
    }
}
