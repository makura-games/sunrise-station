using Content.Server.AlertLevel;
using Content.Shared.CCVar;
using Content.Shared.Communications;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.UserInterface;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемой upstream-системе.
namespace Content.Server.Communications;

public sealed partial class CommunicationsConsoleSystem
{
    /*
     * Additional alert-level controls and remote station selection.
     */

    private void InitializeAlertLevelControls()
    {
        SubscribeLocalEvent<AdditionalAlertLevelChangedEvent>(OnAdditionalAlertLevelChanged);
        SubscribeLocalEvent<CommunicationsConsoleComponent, CommunicationsConsoleSetAdditionalAlertLevelMessage>(OnSetAdditionalAlertLevelMessage);
        SubscribeLocalEvent<CommunicationsConsoleComponent, CommunicationsConsoleSelectAlertStationMessage>(OnSelectAlertStationMessage);
        SubscribeLocalEvent<CommunicationsConsoleComponent, BoundUIOpenedEvent>(OnAlertConsoleOpened);
    }

    private void OnAdditionalAlertLevelChanged(AdditionalAlertLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<CommunicationsConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (args.Station == ResolveAlertStation((uid, component)))
                UpdateCommsConsoleInterface(uid, component);
        }
    }

    private void OnAlertConsoleOpened(
        Entity<CommunicationsConsoleComponent> console,
        ref BoundUIOpenedEvent args)
    {
        EnsureAlertStation(console);
        UpdateCommsConsoleInterface(console, console.Comp);
    }

    /// <summary>
    /// Returns the currently selected valid alert station without changing the console state.
    /// </summary>
    private EntityUid? ResolveAlertStation(Entity<CommunicationsConsoleComponent> console)
    {
        if (!console.Comp.CanSelectAlertStation)
        {
            var owningStation = _stationSystem.GetOwningStation(console);
            return owningStation is { } station && IsValidAlertStation(station)
                ? station
                : null;
        }

        return console.Comp.SelectedAlertStation is { } selected && IsValidAlertStation(selected)
            ? selected
            : null;
    }

    /// <summary>
    /// Selects a fallback alert station when a privileged console has no valid target.
    /// </summary>
    private EntityUid? EnsureAlertStation(Entity<CommunicationsConsoleComponent> console)
    {
        if (!console.Comp.CanSelectAlertStation)
            return ResolveAlertStation(console);

        if (ResolveAlertStation(console) is { } selected)
            return selected;

        var owningStation = _stationSystem.GetOwningStation(console);
        if (owningStation is { } station && IsValidAlertStation(station))
        {
            console.Comp.SelectedAlertStation = station;
            return station;
        }

        foreach (var candidate in _stationSystem.GetStations())
        {
            if (!IsValidAlertStation(candidate))
                continue;

            console.Comp.SelectedAlertStation = candidate;
            return candidate;
        }

        console.Comp.SelectedAlertStation = null;
        return null;
    }

    private List<CommunicationsConsoleAlertStationState> GetAlertStationStates()
    {
        var result = new List<CommunicationsConsoleAlertStationState>();
        foreach (var station in _stationSystem.GetStations())
        {
            if (!IsValidAlertStation(station))
                continue;

            result.Add(new CommunicationsConsoleAlertStationState(
                GetNetEntity(station),
                MetaData(station).EntityName));
        }

        result.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.CurrentCulture));
        return result;
    }

    private List<CommunicationsConsoleAdditionalAlertLevelState> GetAdditionalAlertLevelStates(
        Entity<AlertLevelComponent> station,
        CommunicationsConsoleComponent console)
    {
        var result = new List<CommunicationsConsoleAdditionalAlertLevelState>();
        foreach (var (id, detail) in station.Comp.AlertLevels!.Levels)
        {
            if (!detail.IsAdditional || !IsAlertLevelAllowed(console, id, detail))
                continue;

            var enabled = station.Comp.ActiveAdditionalLevels.Contains(id);
            result.Add(new CommunicationsConsoleAdditionalAlertLevelState(
                id,
                enabled,
                station.Comp.CurrentDelay <= 0 && _alertLevelSystem.CanSetAdditionalLevel(
                    station.AsNullable(),
                    id,
                    !enabled,
                    console.ForceAlertLevelChanges)));
        }

        return result;
    }

    private bool IsValidAlertStation(EntityUid station)
    {
        return HasComp<StationDataComponent>(station)
            && TryComp<AlertLevelComponent>(station, out var alert)
            && alert.AlertLevels != null;
    }

    /// <summary>
    /// Attempts to select a station for alert-level changes made from the specified console.
    /// </summary>
    public bool TrySelectAlertStation(
        Entity<CommunicationsConsoleComponent?> console,
        EntityUid station,
        EntityUid user)
    {
        if (!CanSelectAlertStation(console, station, user))
            return false;

        Resolve(console, ref console.Comp);
        DoSelectAlertStation((console, console.Comp!), station);
        return true;
    }

    /// <summary>
    /// Checks whether the specified console and user may select the station as an alert-level target.
    /// </summary>
    public bool CanSelectAlertStation(
        Entity<CommunicationsConsoleComponent?> console,
        EntityUid station,
        EntityUid user)
    {
        return Resolve(console, ref console.Comp)
            && console.Comp.CanSelectAlertStation
            && CanUse(user, console)
            && IsValidAlertStation(station);
    }

    private static void DoSelectAlertStation(
        Entity<CommunicationsConsoleComponent> console,
        EntityUid station)
    {
        console.Comp.SelectedAlertStation = station;
    }

    /// <summary>
    /// Checks whether this console is configured to control the specified alert level.
    /// </summary>
    public static bool IsAlertLevelAllowed(
        CommunicationsConsoleComponent console,
        string level,
        AlertLevelDetail detail)
    {
        return console.AllowedAlertLevels?.Contains(level) ?? detail.Selectable;
    }

    /// <summary>
    /// Attempts to set the primary alert level through a communications console.
    /// </summary>
    public bool TrySetPrimaryAlertLevel(
        Entity<CommunicationsConsoleComponent?> console,
        string level,
        EntityUid user)
    {
        if (!CanSetPrimaryAlertLevel(console, level, user))
            return false;

        Resolve(console, ref console.Comp);
        return DoSetPrimaryAlertLevel((console, console.Comp!), level);
    }

    /// <summary>
    /// Checks whether a user may set the primary alert level through a communications console.
    /// </summary>
    public bool CanSetPrimaryAlertLevel(
        Entity<CommunicationsConsoleComponent?> console,
        string level,
        EntityUid user,
        bool quiet = false)
    {
        if (!Resolve(console, ref console.Comp))
            return false;

        if (!CanUse(user, console))
        {
            if (!quiet)
            {
                _popupSystem.PopupCursor(
                    Loc.GetString("comms-console-permission-denied"),
                    user,
                    PopupType.Medium);
            }

            return false;
        }

        var station = ResolveAlertStation((console, console.Comp));
        return station is { } stationUid
            && TryComp<AlertLevelComponent>(stationUid, out var alert)
            && alert.AlertLevels != null
            && alert.AlertLevels.Levels.TryGetValue(level, out var detail)
            && !detail.IsAdditional
            && IsAlertLevelAllowed(console.Comp, level, detail)
            && alert.CurrentLevel != level
            && alert.CurrentDelay <= 0
            && (console.Comp.ForceAlertLevelChanges
                || detail.Selectable && _alertLevelSystem.IsSelectable((stationUid, alert)));
    }

    private bool DoSetPrimaryAlertLevel(
        Entity<CommunicationsConsoleComponent> console,
        string level)
    {
        var station = ResolveAlertStation(console);
        if (station is not { } stationUid || !TryComp<AlertLevelComponent>(stationUid, out var alert))
            return false;

        // Привилегированная консоль обходит запрет выбора кода, но не общий cooldown ручных изменений.
        if (console.Comp.ForceAlertLevelChanges)
            StartAlertLevelCooldown(alert);

        _alertLevelSystem.SetLevel(
            stationUid,
            level,
            true,
            true,
            console.Comp.ForceAlertLevelChanges,
            component: alert);
        return true;
    }

    /// <summary>
    /// Attempts to enable or disable an additional alert level from a communications console.
    /// </summary>
    public bool TrySetAdditionalAlertLevel(
        Entity<CommunicationsConsoleComponent?> console,
        string level,
        bool enabled,
        EntityUid user)
    {
        if (!CanSetAdditionalAlertLevel(console, level, enabled, user))
            return false;

        Resolve(console, ref console.Comp);
        return DoSetAdditionalAlertLevel((console, console.Comp!), level, enabled);
    }

    /// <summary>
    /// Checks whether a user may change an additional alert level through a communications console.
    /// </summary>
    public bool CanSetAdditionalAlertLevel(
        Entity<CommunicationsConsoleComponent?> console,
        string level,
        bool enabled,
        EntityUid user,
        bool quiet = false)
    {
        if (!Resolve(console, ref console.Comp))
            return false;

        if (!CanUse(user, console))
        {
            if (!quiet)
            {
                _popupSystem.PopupCursor(
                    Loc.GetString("comms-console-permission-denied"),
                    user,
                    PopupType.Medium);
            }

            return false;
        }

        var station = ResolveAlertStation((console, console.Comp));
        if (station == null
            || !TryComp<AlertLevelComponent>(station.Value, out var alert)
            || alert.AlertLevels == null
            || !alert.AlertLevels.Levels.TryGetValue(level, out var detail)
            || !detail.IsAdditional
            || !IsAlertLevelAllowed(console.Comp, level, detail))
        {
            return false;
        }

        if (console.Comp.ForceAlertLevelChanges && alert.CurrentDelay > 0)
            return false;

        return _alertLevelSystem.CanSetAdditionalLevel(
            (station.Value, alert),
            level,
            enabled,
            console.Comp.ForceAlertLevelChanges);
    }

    private bool DoSetAdditionalAlertLevel(
        Entity<CommunicationsConsoleComponent> console,
        string level,
        bool enabled)
    {
        var station = ResolveAlertStation(console);
        if (station == null || !TryComp<AlertLevelComponent>(station.Value, out var alert))
            return false;

        if (console.Comp.ForceAlertLevelChanges)
            StartAlertLevelCooldown(alert);

        return _alertLevelSystem.TrySetAdditionalLevel(
            station.Value,
            level,
            enabled,
            playSound: true,
            announce: true,
            force: console.Comp.ForceAlertLevelChanges,
            component: alert);
    }

    private void OnSetAdditionalAlertLevelMessage(
        Entity<CommunicationsConsoleComponent> console,
        ref CommunicationsConsoleSetAdditionalAlertLevelMessage message)
    {
        if (message.Actor is not { Valid: true } user)
            return;

        if (!TrySetAdditionalAlertLevel(console.AsNullable(), message.Level, message.Enabled, user))
            UpdateCommsConsoleInterface(console, console.Comp);
    }

    private void StartAlertLevelCooldown(AlertLevelComponent alert)
    {
        alert.CurrentDelay = _cfg.GetCVar(CCVars.GameAlertLevelChangeDelay);
        alert.ActiveDelay = true;
    }

    private void OnSelectAlertStationMessage(
        Entity<CommunicationsConsoleComponent> console,
        ref CommunicationsConsoleSelectAlertStationMessage message)
    {
        if (message.Actor is not { Valid: true } user
            || !TryGetEntity(message.Station, out var stationUid)
            || stationUid is not { } station
            || !TrySelectAlertStation(console.AsNullable(), station, user))
        {
            return;
        }

        UpdateCommsConsoleInterface(console, console.Comp);
    }
}
