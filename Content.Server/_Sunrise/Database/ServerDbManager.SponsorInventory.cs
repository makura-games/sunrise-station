using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Пространство имён соответствует исходному partial-классу.
namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<DatabaseSponsorInventoryProfile?> GetSponsorInventoryProfileAsync(Guid player, int slot, CancellationToken cancel = default);
    Task<Dictionary<int, DatabaseSponsorInventoryProfile>> GetSponsorInventoryProfilesAsync(Guid player, CancellationToken cancel = default);
    Task<DatabaseSponsorInventoryProfile> SaveSponsorInventoryProfileAsync(Guid player, int slot, string profileJson, string revision, DateTimeOffset updatedAt, CancellationToken cancel = default);
    Task<bool> DeleteSponsorInventoryProfileAsync(Guid player, int slot, CancellationToken cancel = default);
}

public sealed record DatabaseSponsorInventoryProfile(
    int Slot,
    string ProfileJson,
    string Revision,
    DateTimeOffset UpdatedAt);

public sealed partial class ServerDbManager
{
    public Task<DatabaseSponsorInventoryProfile?> GetSponsorInventoryProfileAsync(
        Guid player,
        int slot,
        CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetSponsorInventoryProfileAsync(player, slot, cancel));
    }

    public Task<Dictionary<int, DatabaseSponsorInventoryProfile>> GetSponsorInventoryProfilesAsync(
        Guid player,
        CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetSponsorInventoryProfilesAsync(player, cancel));
    }

    public Task<DatabaseSponsorInventoryProfile> SaveSponsorInventoryProfileAsync(
        Guid player,
        int slot,
        string profileJson,
        string revision,
        DateTimeOffset updatedAt,
        CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.SaveSponsorInventoryProfileAsync(player, slot, profileJson, revision, updatedAt, cancel));
    }

    public Task<bool> DeleteSponsorInventoryProfileAsync(
        Guid player,
        int slot,
        CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.DeleteSponsorInventoryProfileAsync(player, slot, cancel));
    }
}
