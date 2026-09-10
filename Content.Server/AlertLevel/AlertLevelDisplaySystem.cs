using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared.AlertLevel;
using Content.Shared.Power;

namespace Content.Server.AlertLevel;

public sealed partial class AlertLevelDisplaySystem : EntitySystem // Sunrise-Edit
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertChanged);
        SubscribeLocalEvent<AdditionalAlertLevelChangedEvent>(OnAdditionalAlertChanged); // Sunrise-Edit
        SubscribeLocalEvent<AlertLevelDisplayComponent, ComponentInit>(OnDisplayInit);
        SubscribeLocalEvent<AlertLevelDisplayComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnAlertChanged(AlertLevelChangedEvent args)
    {
        UpdateDisplays(args.Station); // Sunrise-Edit
    }

    private void OnDisplayInit(EntityUid uid, AlertLevelDisplayComponent alertLevelDisplay, ComponentInit args)
    {
        if (TryComp(uid, out AppearanceComponent? appearance))
        {
            var stationUid = _stationSystem.GetOwningStation(uid);
            if (stationUid is { } station
                && TryComp<AlertLevelComponent>(station, out var alert)
                && _alertLevel.TryGetVisualAlertLevel((station, alert), out var level, out _))
            {
                _appearance.SetData(uid, AlertLevelDisplay.CurrentLevel, level, appearance);
            }
        }
    }
    private void OnPowerChanged(EntityUid uid, AlertLevelDisplayComponent alertLevelDisplay, ref PowerChangedEvent args)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;

        _appearance.SetData(uid, AlertLevelDisplay.Powered, args.Powered, appearance);
    }
}
