using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public partial interface IServerDbManager
{
    /// <summary>
    /// Returns top players by overall playtime.
    /// </summary>
    Task<List<(string Username, TimeSpan Time)>> GetTopPlayersOverall(int count, CancellationToken cancel = default);

    /// <summary>
    /// Adds a playtime session record to the database and returns its ID.
    /// </summary>
    Task<int> AddPlayTimeSessionAsync(Guid playerId, DateTime startTime, DateTime endTime);

    /// <summary>
    /// Updates the end time of a playtime session in the database.
    /// </summary>
    Task UpdatePlayTimeSessionAsync(int sessionId, DateTime endTime);

    /// <summary>
    /// Calculates top players by playtime for the specified period based on sessions.
    /// </summary>
    Task<List<(string Username, TimeSpan Time)>> GetTopPlayersActiveSinceWithSession(
        DateTime since,
        int count,
        CancellationToken cancel = default);
}

public sealed partial class ServerDbManager
{
    public Task<List<(string Username, TimeSpan Time)>> GetTopPlayersOverall(int count, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetTopPlayersOverall(count, cancel));
    }

    public Task<int> AddPlayTimeSessionAsync(Guid playerId, DateTime startTime, DateTime endTime)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.AddPlayTimeSessionAsync(playerId, startTime, endTime));
    }

    public Task UpdatePlayTimeSessionAsync(int sessionId, DateTime endTime)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.UpdatePlayTimeSessionAsync(sessionId, endTime));
    }

    public Task<List<(string Username, TimeSpan Time)>> GetTopPlayersActiveSinceWithSession(
        DateTime since,
        int count,
        CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetTopPlayersActiveSinceWithSession(since, count, cancel));
    }
}
