using System.Threading;
using Content.Server.Chat.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.AlertLevel;
using Content.Shared.GameTicking;
using Content.Shared.Station.Components;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Sunrise.ExtendedAccess;

public sealed partial class ExtendedAccessSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;

    private static CancellationTokenSource _token = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseAlertLevelChangedEvent>(OnAlertLevelChanged);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => RecreateToken());
    }


    /// <summary>
    /// Запускает таймер и выводит объявление о смене доступов через некоторое время
    /// </summary>
    private void OnAlertLevelChanged(ref SunriseAlertLevelChangedEvent ev)
    {
        if (!TryComp<AlertLevelComponent>(ev.Station, out var alert))
            return;

        if (!ProtoMan.Resolve(ev.AlertLevel, out var currentLevelDetail))
            return;

        var options = currentLevelDetail.ExtendedAccessOptions;

        if (options == null)
            return;

        // Предотвращение стаканье смены доступов. Доступы должны сменяться только на последний код угрозы.
        RecreateToken();

        var station = ev.Station;

        Timer.Spawn(options.Value.Delay, () => AfterDelay((station, alert)), _token.Token);

        if (options.Value.Announcement != null)
        {
            // В строке локализации оповещения обязательно должно быть указан параметр для времени
            var message = Loc.GetString(options.Value.Announcement.Value, ("time", options.Value.Delay.TotalSeconds));

            _chat.DispatchStationAnnouncement(ev.Station,
                message,
                colorOverride: Color.Yellow,
                sender: Loc.GetString("access-system-sender"));
        }
    }

    /// <summary>
    /// Проходится по всем сущностям, считывающим доступ.
    /// Заставляет пересмотреть свои доступы в соответствии с текущим кодом угрозы
    /// </summary>
    private void AfterDelay(Entity<AlertLevelComponent> station)
    {
        if (TerminatingOrDeleted(station))
            return;

        if (!_alertLevel.TryGetLevel(station.AsNullable(), out var currentLevel) || currentLevel is not { } level)
            return;

        _chat.DispatchStationAnnouncement(station,
            Loc.GetString("access-system-accesses-established"),
            colorOverride: Color.Yellow,
            sender: Loc.GetString("access-system-sender"));

        var query = EntityQueryEnumerator<AccessReaderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var reader, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != station)
                continue;

            if (reader.AlertAccesses.Count == 0)
                continue;

            _accessReader.UpdateAccess((uid, reader), level.Id.ToLowerInvariant());
        }
    }

    private static void RecreateToken()
    {
        _token.Cancel();
        _token = new();
    }
}
