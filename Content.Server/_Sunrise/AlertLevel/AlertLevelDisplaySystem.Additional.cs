using Content.Server.Power.Components;
using Content.Shared.AlertLevel;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой upstream-системе.
namespace Content.Server.AlertLevel;

public sealed partial class AlertLevelDisplaySystem
{
    /*
     * Display updates for simultaneously active alert levels.
     */

    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;

    private void OnAdditionalAlertChanged(AdditionalAlertLevelChangedEvent args)
    {
        UpdateDisplays(args.Station);
    }

    private void UpdateDisplays(EntityUid station)
    {
        if (!_alertLevel.TryGetVisualAlertLevel((station, null), out var level, out _))
            return;

        var query = EntityQueryEnumerator<AlertLevelDisplayComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out _, out var appearance))
        {
            if (_stationSystem.GetOwningStation(uid) != station)
                continue;

            _appearance.SetData(uid, AlertLevelDisplay.CurrentLevel, level, appearance);
        }
    }
}
