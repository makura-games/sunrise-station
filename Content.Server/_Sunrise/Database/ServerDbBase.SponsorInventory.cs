using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Пространство имён соответствует исходному partial-классу.
namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<DatabaseSponsorInventoryProfile?> GetSponsorInventoryProfileAsync(
        Guid player,
        int slot,
        CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);

        return await db.DbContext.SponsorInventoryProfiles
            .AsNoTracking()
            .Where(profile => profile.PlayerUserId == player)
            .Where(profile => profile.Slot == slot)
            .Select(profile => new DatabaseSponsorInventoryProfile(
                profile.Slot,
                profile.ProfileJson,
                profile.Revision,
                profile.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken: cancel);
    }

    public async Task<Dictionary<int, DatabaseSponsorInventoryProfile>> GetSponsorInventoryProfilesAsync(
        Guid player,
        CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);

        return await db.DbContext.SponsorInventoryProfiles
            .AsNoTracking()
            .Where(profile => profile.PlayerUserId == player)
            .ToDictionaryAsync(
                profile => profile.Slot,
                profile => new DatabaseSponsorInventoryProfile(
                    profile.Slot,
                    profile.ProfileJson,
                    profile.Revision,
                    profile.UpdatedAt),
                cancellationToken: cancel);
    }

    public async Task<DatabaseSponsorInventoryProfile> SaveSponsorInventoryProfileAsync(
        Guid player,
        int slot,
        string profileJson,
        string revision,
        DateTimeOffset updatedAt,
        CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);

        var updated = await db.DbContext.SponsorInventoryProfiles
            .Where(entry => entry.PlayerUserId == player)
            .Where(entry => entry.Slot == slot)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entry => entry.ProfileJson, profileJson)
                .SetProperty(entry => entry.Revision, revision)
                .SetProperty(entry => entry.UpdatedAt, updatedAt),
                cancellationToken: cancel);

        if (updated != 0)
            return new DatabaseSponsorInventoryProfile(slot, profileJson, revision, updatedAt);

        var profile = new SponsorInventoryProfile
        {
            PlayerUserId = player,
            Slot = slot,
            ProfileJson = profileJson,
            Revision = revision,
            UpdatedAt = updatedAt,
        };

        db.DbContext.SponsorInventoryProfiles.Add(profile);

        try
        {
            await db.DbContext.SaveChangesAsync(cancel);
        }
        catch (DbUpdateException)
        {
            db.DbContext.Entry(profile).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

            updated = await db.DbContext.SponsorInventoryProfiles
                .Where(entry => entry.PlayerUserId == player)
                .Where(entry => entry.Slot == slot)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(entry => entry.ProfileJson, profileJson)
                        .SetProperty(entry => entry.Revision, revision)
                        .SetProperty(entry => entry.UpdatedAt, updatedAt),
                    cancellationToken: cancel);

            if (updated != 0)
                return new DatabaseSponsorInventoryProfile(slot, profileJson, revision, updatedAt);

            throw;
        }

        return new DatabaseSponsorInventoryProfile(slot, profileJson, revision, updatedAt);
    }

    public async Task<bool> DeleteSponsorInventoryProfileAsync(
        Guid player,
        int slot,
        CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);

        var profile = await db.DbContext.SponsorInventoryProfiles
            .Where(entry => entry.PlayerUserId == player)
            .Where(entry => entry.Slot == slot)
            .SingleOrDefaultAsync(cancellationToken: cancel);

        if (profile == null)
            return false;

        db.DbContext.SponsorInventoryProfiles.Remove(profile);
        await db.DbContext.SaveChangesAsync(cancel);
        return true;
    }
}
