using System.Threading;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Station.Components;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Sunrise.ExtendedAccess;

public sealed partial class ExtendedAccessSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;

    private static CancellationTokenSource _token = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => RecreateToken());
    }


    /// <summary>
    /// Запускает таймер и выводит объявление о смене доступов через некоторое время
    /// </summary>
    private void OnAlertLevelChanged(AlertLevelChangedEvent ev)
    {
        // Это случай первичного установления кода(зеленый) по умолчанию
        // Чтобы в начале раунда не слышать, что доступы изменились на зеленый
        if (ev.PreviousLevel == string.Empty)
            return;

        if (!TryComp<AlertLevelComponent>(ev.Station, out var alert))
            return;

        if (alert.AlertLevels == null)
            return;

        if (!alert.AlertLevels.Levels.TryGetValue(alert.CurrentLevel, out var currentLevelDetail))
            return;

        var options = currentLevelDetail.ExtendedAccessOptions;

        if (options == null)
            return;

        // Предотвращение стаканье смены доступов. Доступы должны сменяться только на последний код угрозы.
        RecreateToken();

        Timer.Spawn(options.Value.Delay, () => AfterDelay((ev.Station, alert)), _token.Token);

        if (options.Value.Announcement != null)
        {
            // В строке локализации оповещения обязательно должно быть указан параметр для времени
            var message = Loc.GetString(options.Value.Announcement, ("time", options.Value.Delay.TotalSeconds));

            _chat.DispatchStationAnnouncement(ev.Station,
                Loc.GetString(message),
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

            _accessReader.UpdateAccess((uid, reader), station.Comp.CurrentLevel);
        }
    }

    private static void RecreateToken()
    {
        _token.Cancel();
        _token = new();
    }
}
