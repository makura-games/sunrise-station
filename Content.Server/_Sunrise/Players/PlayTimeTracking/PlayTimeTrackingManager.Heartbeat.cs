using System;
using System.Threading.Tasks;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Network;
using Robust.Shared.Player;

#pragma warning disable IDE0130
namespace Content.Server.Players.PlayTimeTracking;

public sealed partial class PlayTimeTrackingManager
{
    private static readonly TimeSpan PlayTimeSessionHeartbeatInterval = TimeSpan.FromMinutes(1);

    private void UpdatePlayTimeSessions()
    {
        var time = _timing.RealTime;

        foreach (var (_, data) in _playTimeData)
        {
            if (!data.IsSessionActive || !data.CurrentDbSessionId.HasValue)
                continue;

            if (time < data.NextHeartbeat)
                continue;

            data.NextHeartbeat = time + PlayTimeSessionHeartbeatInterval;
            TrackPending(UpdatePlayTimeSessionSafeAsync(data.CurrentDbSessionId.Value, DateTime.UtcNow));
        }
    }

    private void UpdatePlayTimeSessionActivity(ICommonSession session, PlayTimeData data)
    {
        var shouldBeActive = session.Channel.AuthType == LoginType.LoggedIn &&
                             data.ActiveTrackers.Contains(PlayTimeTrackingShared.TrackerOverall);

        if (shouldBeActive && !data.IsSessionActive)
        {
            data.IsSessionActive = true;
            var generation = ++data.SessionGeneration;
            TrackPending(StartPlayTimeSessionAsync(session, data, generation));
        }
        else if (!shouldBeActive && data.IsSessionActive)
        {
            EndPlayTimeSession(data);
        }
    }

    private async Task StartPlayTimeSessionAsync(ICommonSession session, PlayTimeData data, int generation)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            var sessionId = await _db.AddPlayTimeSessionAsync(session.UserId.UserId, startTime, startTime);
            if (data.SessionGeneration != generation || !data.IsSessionActive)
            {
                var endTime = data.LastSessionEndTime > startTime ? data.LastSessionEndTime : startTime;
                await _db.UpdatePlayTimeSessionAsync(sessionId, endTime);
                return;
            }

            data.CurrentDbSessionId = sessionId;
            data.NextHeartbeat = _timing.RealTime + PlayTimeSessionHeartbeatInterval;
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to create playtime session for {session.Name}: {e}");
        }
    }

    private void EndPlayTimeSession(PlayTimeData data)
    {
        if (!data.IsSessionActive)
            return;

        data.IsSessionActive = false;
        data.LastSessionEndTime = DateTime.UtcNow;
        data.SessionGeneration++;

        if (data.CurrentDbSessionId.HasValue)
        {
            var sessionId = data.CurrentDbSessionId.Value;
            data.CurrentDbSessionId = null;
            TrackPending(UpdatePlayTimeSessionSafeAsync(sessionId, data.LastSessionEndTime));
        }
    }

    private async Task UpdatePlayTimeSessionSafeAsync(int sessionId, DateTime endTime)
    {
        try
        {
            await _db.UpdatePlayTimeSessionAsync(sessionId, endTime);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to update playtime session {sessionId}: {e}");
        }
    }

    private void OnPlayTimeSessionDisconnected(ICommonSession session)
    {
        if (_playTimeData.TryGetValue(session, out var data))
        {
            EndPlayTimeSession(data);
        }
    }

    private sealed partial class PlayTimeData
    {
        /// <summary>
        /// Current playtime session database ID.
        /// </summary>
        public int? CurrentDbSessionId;

        /// <summary>
        /// Next heartbeat timestamp for the playtime session.
        /// </summary>
        public TimeSpan NextHeartbeat;

        /// <summary>
        /// Whether an active Overall playtime session is currently running for this player.
        /// </summary>
        public bool IsSessionActive;

        /// <summary>
        /// Monotonic generation counter to handle async session start/stop races.
        /// </summary>
        public int SessionGeneration;

        /// <summary>
        /// Timestamp when the most recent active session ended.
        /// </summary>
        public DateTime LastSessionEndTime;
    }
}
