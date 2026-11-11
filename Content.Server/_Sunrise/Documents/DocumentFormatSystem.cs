using Content.Server.GameTicking.Events;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Documents;

public sealed partial class DocumentFormatSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StationSystem _station = default!;

    private TimeSpan _roundStart;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundStartingEvent>(_ => _roundStart = _timing.CurTime);
    }

    public string Format(string content, EntityUid context)
    {
        var offsH = _cfg.GetCVar(SunriseCCVars.DocumentTimeOffsetHours);
        var offsY = _cfg.GetCVar(SunriseCCVars.DocumentYearOffset);

        var date = DateTime.UtcNow.AddHours(offsH).AddYears(offsY).ToString("dd.MM.yyyy");
        var shift = _timing.CurTime - _roundStart;
        var timeString = $"{shift:hh\\:mm} {date}";

        var st = _station.GetOwningStation(context);
        var stationName = st is null ? string.Empty : Name(st.Value);

        var corp = _cfg.GetCVar(SunriseCCVars.DocumentCorporationName);

        return content
            .Replace("{timeString}", timeString)
            .Replace("{stationName}", stationName)
            .Replace("{corpString}", corp);
    }
}
