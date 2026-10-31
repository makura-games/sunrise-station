using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Players.PlayTimeTracking;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<List<(string Username, TimeSpan Time)>> GetTopPlayersOverall(int count, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var query = from pt in db.DbContext.PlayTime.AsNoTracking()
            where pt.Tracker == PlayTimeTrackingShared.TrackerOverall
            join p in db.DbContext.Player.AsNoTracking() on pt.PlayerId equals p.UserId
            select new { p.LastSeenUserName, pt.TimeSpent };

        if (db.DbContext is PostgresServerDbContext)
        {
            var pgResults = await query
                .OrderByDescending(x => x.TimeSpent)
                .Take(count)
                .ToListAsync(cancel);

            return pgResults
                .Select(x => ValueTuple.Create(x.LastSeenUserName, x.TimeSpent))
                .ToList();
        }

        // SQLite does not support TimeSpan in ORDER BY clauses; sort in memory for dev/test portability.
        var sqliteResults = await query.ToListAsync(cancel);
        return sqliteResults
            .OrderByDescending(x => x.TimeSpent)
            .Take(count)
            .Select(x => ValueTuple.Create(x.LastSeenUserName, x.TimeSpent))
            .ToList();
    }

    public async Task<int> AddPlayTimeSessionAsync(Guid playerId, DateTime startTime, DateTime endTime)
    {
        await using var db = await GetDb();

        var session = new PlayTimeSession
        {
            PlayerId = playerId,
            StartTime = startTime,
            EndTime = endTime
        };

        db.DbContext.PlayTimeSession.Add(session);
        await db.DbContext.SaveChangesAsync();

        return session.Id;
    }

    public async Task UpdatePlayTimeSessionAsync(int sessionId, DateTime endTime)
    {
        await using var db = await GetDb();

        var session = await db.DbContext.PlayTimeSession.FindAsync(sessionId);
        if (session != null)
        {
            session.EndTime = endTime;
            await db.DbContext.SaveChangesAsync();
        }
    }

    public async Task<List<(string Username, TimeSpan Time)>> GetTopPlayersActiveSinceWithSession(
        DateTime since,
        int count,
        CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var sessions = await db.DbContext.PlayTimeSession
            .AsNoTracking()
            .Where(pts => pts.EndTime >= since)
            .Select(pts => new { pts.PlayerId, pts.StartTime, pts.EndTime })
            .ToListAsync(cancel);

        var topById = sessions
            .GroupBy(x => x.PlayerId)
            .Select(g =>
            {
                long totalTicks = 0;
                foreach (var s in g)
                {
                    var clampedStart = s.StartTime < since ? since : s.StartTime;
                    if (s.EndTime > clampedStart)
                        totalTicks += (s.EndTime - clampedStart).Ticks;
                }

                return (PlayerId: g.Key, Ticks: totalTicks);
            })
            .Where(x => x.Ticks > 0)
            .OrderByDescending(x => x.Ticks)
            .Take(count)
            .ToList();

        if (topById.Count == 0)
            return [];

        var topPlayerIds = topById.Select(x => x.PlayerId).ToList();
        var names = await db.DbContext.Player
            .AsNoTracking()
            .Where(p => topPlayerIds.Contains(p.UserId))
            .Select(p => new { p.UserId, p.LastSeenUserName })
            .ToDictionaryAsync(p => p.UserId, p => p.LastSeenUserName, cancel);

        var result = new List<(string Username, TimeSpan Time)>(topById.Count);
        foreach (var (playerId, ticks) in topById)
        {
            if (names.TryGetValue(playerId, out var username) && !string.IsNullOrWhiteSpace(username))
                result.Add((username, TimeSpan.FromTicks(ticks)));
        }

        return result;
    }
}
