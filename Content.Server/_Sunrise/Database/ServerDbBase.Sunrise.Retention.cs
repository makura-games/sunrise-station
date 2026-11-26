using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

// ReSharper disable once CheckNamespace
namespace Content.Server.Database;

/// <summary>
/// Число уникальных и новых игроков сервера за окно времени.
/// </summary>
public readonly record struct AudienceCounts(int UniquePlayers, int NewPlayers);

/// <summary>
/// Успешное подключение игрока к серверу вместе с датой его первого появления в базе.
/// </summary>
public readonly record struct NewPlayerConnection(Guid UserId, DateTime FirstSeenTime, DateTime ConnectionTime);

public abstract partial class ServerDbBase
{
    public async Task<AudienceCounts> GetAudienceCountsAsync(
        int serverId,
        DateTime since,
        CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var connections = db.DbContext.ConnectionLog
            .AsNoTracking()
            .Where(log => log.ServerId == serverId && log.Denied == null && log.Time >= since);

        var unique = await connections
            .Select(log => log.UserId)
            .Distinct()
            .CountAsync(cancel);

        // Новым считается игрок, впервые появившийся в базе внутри окна.
        var newPlayers = await (
                from log in connections
                join player in db.DbContext.Player on log.UserId equals player.UserId
                where player.FirstSeenTime >= since
                select log.UserId)
            .Distinct()
            .CountAsync(cancel);

        return new AudienceCounts(unique, newPlayers);
    }

    public async Task<List<NewPlayerConnection>> GetNewPlayerConnectionsAsync(
        int serverId,
        DateTime firstSeenFrom,
        DateTime firstSeenTo,
        CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var rows = await (
                from log in db.DbContext.ConnectionLog.AsNoTracking()
                join player in db.DbContext.Player.AsNoTracking() on log.UserId equals player.UserId
                where player.FirstSeenTime >= firstSeenFrom &&
                      player.FirstSeenTime < firstSeenTo &&
                      log.ServerId == serverId &&
                      log.Denied == null &&
                      log.Time >= firstSeenFrom
                select new { log.UserId, player.FirstSeenTime, log.Time })
            .ToListAsync(cancel);

        var result = new List<NewPlayerConnection>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(new NewPlayerConnection(
                row.UserId,
                NormalizeDatabaseTime(row.FirstSeenTime),
                NormalizeDatabaseTime(row.Time)));
        }

        return result;
    }

    public async Task<bool> TryAddNewPlayerOnboardingStepAsync(
        Guid player,
        int serverId,
        string step,
        DateTime reachedAt,
        CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var exists = await db.DbContext.NewPlayerOnboardingSteps
            .AnyAsync(entry => entry.PlayerUserId == player &&
                               entry.ServerId == serverId &&
                               entry.Step == step,
                cancel);

        if (exists)
            return false;

        db.DbContext.NewPlayerOnboardingSteps.Add(new NewPlayerOnboardingStep
        {
            PlayerUserId = player,
            ServerId = serverId,
            Step = step,
            ReachedAt = reachedAt,
        });

        try
        {
            await db.DbContext.SaveChangesAsync(cancel);
            return true;
        }
        catch (DbUpdateException)
        {
            // Параллельная запись того же шага уже заняла первичный ключ.
            return false;
        }
    }
}
