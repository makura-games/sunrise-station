using Content.Shared.PDA;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-интерфейсу.
namespace Content.Client.PDA;

public sealed partial class PdaMenu
{
    /*
     * Rendering of primary and additional station alert levels.
     */

    private void UpdateAlertLevelState(PdaIdInfoText state)
    {
        IReadOnlyList<PdaAlertLevelInfo> activeAlertLevels = state.StationAlertLevels is { } stationAlertLevels
            ? stationAlertLevels
            : Array.Empty<PdaAlertLevelInfo>();

        if (activeAlertLevels.Count == 0)
            activeAlertLevels = [new PdaAlertLevelInfo(state.StationAlertLevel ?? "unknown", state.StationAlertColor)];

        var localizedAlertLevels = new List<string>(activeAlertLevels.Count);
        var instructions = new List<string>(activeAlertLevels.Count);
        foreach (var activeAlertLevel in activeAlertLevels)
        {
            var key = $"alert-level-{activeAlertLevel.Level}";
            var localizedLevel = FormattedMessage.EscapeText(Loc.GetString(key));
            localizedAlertLevels.Add($"[color={activeAlertLevel.Color.ToHex()}]{localizedLevel}[/color]");
            instructions.Add(Loc.GetString($"{key}-instructions"));
        }

        _alertLevel = string.Join(", ", localizedAlertLevels);
        _instructions = string.Join("\n", instructions);
    }
}
