using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking;
using Content.Server._Sunrise.StationEvents.Events;

namespace Content.Server.AlertLevel;

public sealed partial class AlertLevelSystem : EntitySystem // Sunrise-Edit
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    // Sunrise-Start
    [Dependency] private readonly GameTicker _gameTicker = default!;
    // Sunrise-End

    // Until stations are a prototype, this is how it's going to have to be.
    public const string DefaultAlertLevelSet = "stationAlerts";
    // Sunrise-Start
    private const string EpsilonAlertLevel = "epsilon";
    private const string EpsilonBorgLawChanges = "EpsilonDeathSquadLawset";
    // Sunrise-End

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialize);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeReload);
    }
    public override void Update(float time)
    {
        var query = EntityQueryEnumerator<AlertLevelComponent>();

        while (query.MoveNext(out var station, out var alert))
        {
            if (alert.CurrentDelay <= 0)
            {
                if (alert.ActiveDelay)
                {
                    RaiseLocalEvent(new AlertLevelDelayFinishedEvent());
                    alert.ActiveDelay = false;
                }
                continue;
            }
            alert.CurrentDelay -= time;
        }
    }
    private void OnStationInitialize(StationInitializedEvent args)
    {
        if (!TryComp<AlertLevelComponent>(args.Station, out var alertLevelComponent))
            return;
        if (!_prototypeManager.TryIndex(alertLevelComponent.AlertLevelPrototype, out AlertLevelPrototype? alerts))
        {
            return;
        }
        alertLevelComponent.AlertLevels = alerts;
        var defaultLevel = alertLevelComponent.AlertLevels.DefaultLevel;
        if (string.IsNullOrEmpty(defaultLevel))
        {
            // Deterministic selection of defaultLevel
            defaultLevel = alertLevelComponent.AlertLevels.Levels.Keys.OrderBy(k => k).First();
        }
        SetLevel(args.Station, defaultLevel, false, false, true);
    }
    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        if (!args.ByType.TryGetValue(typeof(AlertLevelPrototype), out var alertPrototypes)
            || !alertPrototypes.Modified.TryGetValue(DefaultAlertLevelSet, out var alertObject)
            || alertObject is not AlertLevelPrototype alerts)
        {
            return;
        }
        var query = EntityQueryEnumerator<AlertLevelComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.AlertLevels = alerts;
            PruneAdditionalLevels((uid, comp)); // Sunrise-Edit
            if (!comp.AlertLevels.Levels.ContainsKey(comp.CurrentLevel))
            {
                var defaultLevel = comp.AlertLevels.DefaultLevel;
                if (string.IsNullOrEmpty(defaultLevel))
                {
                    // Deterministic selection of defaultLevel
                    defaultLevel = comp.AlertLevels.Levels.Keys.OrderBy(k => k).First();
                }
                SetLevel(uid, defaultLevel, true, true, true);
            }
        }
        RaiseLocalEvent(new AlertLevelPrototypeReloadedEvent());
    }
    public string GetLevel(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (!Resolve(station, ref alert))
        {
            return string.Empty;
        }
        return alert.CurrentLevel;
    }
    public float GetAlertLevelDelay(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (!Resolve(station, ref alert))
        {
            return float.NaN;
        }
        return alert.CurrentDelay;
    }
    /// <summary>
    /// Get the default alert level for a station entity.
    /// Returns an empty string if the station has no alert levels defined.
    /// </summary>
    /// <param name="station">The station entity.</param>
    public string GetDefaultLevel(Entity<AlertLevelComponent?> station)
    {
        if (!Resolve(station.Owner, ref station.Comp) || station.Comp.AlertLevels == null)
        {
            return string.Empty;
        }
        return station.Comp.AlertLevels.DefaultLevel;
    }
    /// <summary>
    /// Set the alert level based on the station's entity ID.
    /// </summary>
    /// <param name="station">Station entity UID.</param>
    /// <param name="level">Level to change. Additional levels are always enabled without replacing the primary level; use <see cref="TrySetAdditionalLevel"/> to disable them.</param>
    /// <param name="playSound">Play the alert level's sound.</param>
    /// <param name="announce">Say the alert level's announcement.</param>
    /// <param name="force">Force the alert change. This applies if the alert level is not selectable or not.</param>
    /// <param name="locked">Will it be possible to change level by crew.</param>
    public void SetLevel(EntityUid station, string level, bool playSound, bool announce, bool force = false,
        bool locked = false, MetaDataComponent? dataComponent = null, AlertLevelComponent? component = null)
    {
        if (!Resolve(station, ref component, ref dataComponent)
            || component.AlertLevels == null
            || !component.AlertLevels.Levels.TryGetValue(level, out var detail))
        {
            return;
        }

        // Sunrise edit start - дополнительные коды не заменяют основной
        if (detail.IsAdditional)
        {
            TrySetAdditionalLevel(station, level, true, playSound, announce, force, component);
            return;
        }

        if (component.CurrentLevel == level)
            return;
        // Sunrise edit end
        if (!force)
        {
            if (!detail.Selectable
                || component.CurrentDelay > 0
                || !IsSelectable((station, component))) // Sunrise-Edit
            {
                return;
            }
            component.CurrentDelay = _cfg.GetCVar(CCVars.GameAlertLevelChangeDelay);
            component.ActiveDelay = true;
        }
        // Save previous level for auto access system
        var previousLevel = component.CurrentLevel;
        component.CurrentLevel = level;
        component.IsLevelLocked = locked;
        var stationName = dataComponent.EntityName;
        var name = level.ToLower();
        if (Loc.TryGetString($"alert-level-{level}", out var locName))
        {
            name = locName.ToLower();
        }
        // Announcement text. Is passed into announcementFull.
        var announcement = detail.Announcement;
        if (Loc.TryGetString(detail.Announcement, out var locAnnouncement))
        {
            announcement = locAnnouncement;
        }
        // The full announcement to be spat out into chat.
        var announcementFull = Loc.GetString("alert-level-announcement", ("name", name), ("announcement", announcement));
        var playDefault = false;
        if (playSound)
        {
            if (detail.Sound == null)
                playDefault = true;
        }
        if (announce)
        {
            _chatSystem.DispatchStationAnnouncement(station,
                announcementFull,
                announcementSound: detail.Sound, // Sunrise-edit,
                playDefault: playDefault,
                colorOverride: detail.Color,
                sender: stationName);
        }
        ApplySpecialAlertLevelBehavior(station, level, detail); // Sunrise-Edit
        // Sunrise edit - добавил прежний уровень для системы автодоступов
        // Raise event with previous level for auto access system
        RaiseLocalEvent(new AlertLevelChangedEvent(station, level, previousLevel));
    }
}
public sealed class AlertLevelDelayFinishedEvent : EntityEventArgs
{}
public sealed class AlertLevelPrototypeReloadedEvent : EntityEventArgs
{}
// Sunrise-Start
public sealed class AlertLevelChangedEvent : EntityEventArgs
{
    public EntityUid Station { get; }
    public string AlertLevel { get; }
    public string PreviousLevel { get; } // Sunrise: previous level for auto access system
    public AlertLevelChangedEvent(EntityUid station, string alertLevel, string previousLevel)
    {
        Station = station;
        AlertLevel = alertLevel;
        PreviousLevel = previousLevel;
    }
}
// Sunrise-End
