using Content.Shared.AlertLevel;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace не соответствует структуре папок.
namespace Content.Server.GameTicking.Rules;

public sealed partial class RevolutionaryRuleSystem
{
    [Dependency] private AlertLevelSystem _sunriseAlertLevel = default!;

    private static readonly ProtoId<AlertLevelPrototype> EpsilonAlertLevel = "Epsilon";

    private void HandleSunriseCommandLoss()
    {
        foreach (var station in _stationSystem.GetStations())
        {
            _sunriseAlertLevel.SetLevel(station, EpsilonAlertLevel, true, true, true);
        }

        _roundEnd.EndRound();
    }
}
