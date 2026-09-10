using System.Threading;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Sunrise.ExtendedAccess;

public sealed class ExtendedAccessSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;

    private readonly Dictionary<EntityUid, CancellationTokenSource> _tokens = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
        SubscribeLocalEvent<AdditionalAlertLevelChangedEvent>(OnAdditionalAlertLevelChanged);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => CancelAllUpdates());
    }


    /// <summary>
    /// Schedules an update of temporary access groups after an alert level change.
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

        if (currentLevelDetail.ExtendedAccessOptions is not { } options)
            return;

        ScheduleAccessUpdate((ev.Station, alert), options);
    }

    private void OnAdditionalAlertLevelChanged(AdditionalAlertLevelChangedEvent ev)
    {
        if (!TryComp<AlertLevelComponent>(ev.Station, out var alert)
            || alert.AlertLevels == null
            || !alert.AlertLevels.Levels.TryGetValue(ev.AlertLevel, out var detail)
            || detail.ExtendedAccessOptions is not { } options)
        {
            return;
        }

        ScheduleAccessUpdate((ev.Station, alert), options, ev.Enabled);
    }

    private void ScheduleAccessUpdate(
        Entity<AlertLevelComponent> station,
        ExtendedAccessOptions options,
        bool announceAccessGrant = true)
    {
        // Отменяем отложенное изменение только на этой станции: применяется последнее состояние всех кодов.
        CancelUpdate(station);
        var token = new CancellationTokenSource();
        _tokens[station] = token;

        Timer.Spawn(options.Delay, () => AfterDelay(station, token, announceAccessGrant), token.Token);

        if (announceAccessGrant && options.Announcement != null)
        {
            // В строке локализации оповещения обязательно должно быть указан параметр для времени
            var message = Loc.GetString(options.Announcement, ("time", options.Delay.TotalSeconds));

            _chat.DispatchStationAnnouncement(station,
                message,
                colorOverride: Color.Yellow,
                sender: Loc.GetString("access-system-sender"));
        }
    }

    /// <summary>
    /// Applies the combined temporary access groups from the primary and additional alert levels.
    /// </summary>
    private void AfterDelay(
        Entity<AlertLevelComponent> station,
        CancellationTokenSource token,
        bool announceAccessGrant)
    {
        if (TerminatingOrDeleted(station)
            || !_tokens.TryGetValue(station, out var currentToken)
            || currentToken != token)
        {
            return;
        }

        _tokens.Remove(station);
        token.Dispose();

        if (announceAccessGrant)
        {
            _chat.DispatchStationAnnouncement(station,
                Loc.GetString("access-system-accesses-established"),
                colorOverride: Color.Yellow,
                sender: Loc.GetString("access-system-sender"));
        }

        var activeLevels = _alertLevel.GetActiveLevels(station.AsNullable());
        var globalGroups = new HashSet<ProtoId<AccessGroupPrototype>>();
        foreach (var level in activeLevels)
        {
            if (station.Comp.AlertLevels!.Levels.TryGetValue(level, out var detail)
                && detail.ExtendedAccessOptions?.AccessGroup is { } group)
            {
                globalGroups.Add(group);
            }
        }

        var query = EntityQueryEnumerator<AccessReaderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var reader, out var xform))
        {
            if (CompOrNull<StationMemberComponent>(xform.GridUid)?.Station != station)
                continue;

            if (reader.AlertAccesses.Count == 0)
                continue;

            _accessReader.UpdateAccess(
                (uid, reader),
                station.Comp.CurrentLevel,
                activeLevels,
                globalGroups);
        }
    }

    private void CancelUpdate(EntityUid station)
    {
        if (!_tokens.Remove(station, out var token))
            return;

        token.Cancel();
        token.Dispose();
    }

    private void CancelAllUpdates()
    {
        foreach (var token in _tokens.Values)
        {
            token.Cancel();
            token.Dispose();
        }

        _tokens.Clear();
    }
}
