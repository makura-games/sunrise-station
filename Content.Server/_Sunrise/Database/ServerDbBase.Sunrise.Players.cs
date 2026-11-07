using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<Dictionary<Guid, string>> GetPlayerNamesBatchAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancel)
    {
        await using var db = await GetDb();

        var userIdList = userIds.ToList();
        if (userIdList.Count == 0)
            return new Dictionary<Guid, string>();

        var records = await db.DbContext.Player
            .Where(player => userIdList.Contains(player.UserId))
            .Select(player => new { player.UserId, player.LastSeenUserName })
            .ToListAsync(cancel);

        var result = new Dictionary<Guid, string>();
        foreach (var record in records)
        {
            if (!string.IsNullOrWhiteSpace(record.LastSeenUserName))
                result[record.UserId] = record.LastSeenUserName;
        }

        foreach (var userId in userIdList)
        {
            if (!result.ContainsKey(userId))
                result[userId] = "Unknown";
        }

        return result;
    }
}
