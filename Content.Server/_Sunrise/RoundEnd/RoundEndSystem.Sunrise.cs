using Content.Server._Sunrise.TransitHub;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Timer = Robust.Shared.Timing.Timer;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.RoundEnd;

public sealed partial class RoundEndSystem
{
    /* Интеграция эвакуационного шаттла с транзитным хабом и механиками Sunrise. */

    /// <summary>
    /// Gets the map entity of the Sunrise transit hub.
    /// </summary>
    public EntityUid? GetTransitHub()
    {
        var query = AllEntityQuery<StationTransitHubComponent>();

        while (query.MoveNext(out var transitHub))
        {
            if (Exists(transitHub.MapEntity))
                return transitHub.MapEntity;
        }

        return null;
    }

    private EntityUid? GetSunriseShuttleSourceMap()
    {
        return GetTransitHub();
    }

    private static bool ShouldPlayDefaultSunriseShuttleSounds()
    {
        return false;
    }

    /// <summary>
    /// Gets the time at which the next automatic shuttle call may occur.
    /// </summary>
    public TimeSpan TimeToCallShuttle()
    {
        var autoCallMinutes = _autoCalledBefore
            ? _cfg.GetCVar(CCVars.EmergencyShuttleAutoCallExtensionTime)
            : _cfg.GetCVar(CCVars.EmergencyShuttleAutoCallTime);
        return AutoCallStartTime + TimeSpan.FromMinutes(autoCallMinutes);
    }

    /// <summary>
    /// Delays an active evacuation shuttle countdown.
    /// </summary>
    public void DelayCursedShuttle(TimeSpan delay)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        if (_countdownTokenSource == null || ExpectedCountdownEnd is not { } expectedEnd)
            return;

        var countdown = expectedEnd - _gameTiming.CurTime + delay;
        ExpectedCountdownEnd = _gameTiming.CurTime + countdown;

        _countdownTokenSource.Cancel();
        _countdownTokenSource = new();
        Timer.Spawn(countdown, _shuttle.DockEmergencyShuttle, _countdownTokenSource.Token);

        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString("round-end-system-shuttle-curse-delayed-announcement"),
            Loc.GetString("Station"),
            colorOverride: Color.Gold);
    }

    /// <summary>
    /// Returns whether an evacuation shuttle countdown is active.
    /// </summary>
    public bool ShuttleCalled()
    {
        return ExpectedCountdownEnd != null;
    }

    /// <summary>
    /// Replaces an active countdown and calls the evacuation shuttle with the supplied duration.
    /// </summary>
    public void ForceSetCountdown(TimeSpan countdownTime, bool cantRecall = true)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        _countdownTokenSource?.Cancel();
        _countdownTokenSource = null;

        RequestRoundEnd(countdownTime, checkCooldown: false, cantRecall: cantRecall);
    }
}
