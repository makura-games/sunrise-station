using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Players.PlayTimeTracking;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public sealed partial class ServerDbPostgres
{
    public override async Task<List<(string Username, TimeSpan Time)>> GetTopPlayersOverall(int count, CancellationToken cancel)
    {
        await using var db = await GetDbImpl();

        var trackerOverall = PlayTimeTrackingShared.TrackerOverall.Id;
        var results = await (
            from pt in db.PgDbContext.PlayTime.AsNoTracking()
            where pt.Tracker == trackerOverall
            join p in db.PgDbContext.Player.AsNoTracking() on pt.PlayerId equals p.UserId
            orderby pt.TimeSpent descending
            select new { p.LastSeenUserName, pt.TimeSpent }
        ).Take(count).ToListAsync(cancel);

        return results
            .Select(x => ValueTuple.Create(x.LastSeenUserName, x.TimeSpent))
            .ToList();
    }
}
