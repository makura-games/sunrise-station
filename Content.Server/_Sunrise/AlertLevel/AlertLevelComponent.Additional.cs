#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-компоненту.
namespace Content.Server.AlertLevel;

public sealed partial class AlertLevelComponent
{
    /// <summary>
    /// Additional alert levels currently active alongside the station's primary alert level.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public readonly HashSet<string> ActiveAdditionalLevels = [];
}
