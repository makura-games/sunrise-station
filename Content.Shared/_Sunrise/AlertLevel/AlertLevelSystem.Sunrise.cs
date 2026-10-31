using Robust.Shared.Prototypes;

#pragma warning disable IDE0130
namespace Content.Shared.AlertLevel;

public sealed partial class AlertLevelSystem
{
    private void RaiseSunriseAlertLevelChanged(
        EntityUid station,
        ProtoId<AlertLevelPrototype> level,
        ProtoId<AlertLevelPrototype> previousLevel)
    {
        var ev = new SunriseAlertLevelChangedEvent(station, level, previousLevel);
        RaiseLocalEvent(ref ev);
    }
}

[ByRefEvent]
public record struct SunriseAlertLevelChangedEvent(
    EntityUid Station,
    ProtoId<AlertLevelPrototype> AlertLevel,
    ProtoId<AlertLevelPrototype> PreviousLevel);
