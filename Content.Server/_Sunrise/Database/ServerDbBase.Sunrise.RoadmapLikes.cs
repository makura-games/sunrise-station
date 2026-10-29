using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<List<UiLikeEntryData>> GetUiLikeEntriesAsync(string scopeId, IReadOnlyList<string> itemIds)
    {
        await using var db = await GetDb();

        var normalizedScopeId = NormalizeScopeId(scopeId);
        var normalizedIds = NormalizeUiLikeItemIds(itemIds);

        if (normalizedScopeId == null || normalizedIds.Count == 0)
            return [];

        return await db.DbContext.UiLikes
            .AsNoTracking()
            .Where(like => like.ScopeId == normalizedScopeId && normalizedIds.Contains(like.ItemId))
            .Select(like => new UiLikeEntryData(like.ItemId, like.PlayerUserId))
            .ToListAsync();
    }

    public async Task<List<UiLikeData>> GetUiLikesAsync(Guid player, string scopeId, IReadOnlyList<string> itemIds)
    {
        await using var db = await GetDb();

        var normalizedScopeId = NormalizeScopeId(scopeId);
        var normalizedIds = NormalizeUiLikeItemIds(itemIds);

        if (normalizedScopeId == null || normalizedIds.Count == 0)
            return [];

        var likes = await db.DbContext.UiLikes
            .AsNoTracking()
            .Where(like => like.ScopeId == normalizedScopeId && normalizedIds.Contains(like.ItemId))
            .Select(like => new
            {
                like.ItemId,
                IsLikedByPlayer = like.PlayerUserId == player
            })
            .ToListAsync();

        var counts = new Dictionary<string, int>(likes.Count);
        var likedByPlayer = new HashSet<string>();

        foreach (var like in likes)
        {
            counts[like.ItemId] = counts.GetValueOrDefault(like.ItemId) + 1;

            if (like.IsLikedByPlayer)
                likedByPlayer.Add(like.ItemId);
        }

        var result = new List<UiLikeData>(normalizedIds.Count);
        foreach (var itemId in normalizedIds)
        {
            result.Add(new UiLikeData(
                itemId,
                counts.GetValueOrDefault(itemId),
                likedByPlayer.Contains(itemId)));
        }

        return result;
    }

    public async Task<bool> ToggleUiLikeAsync(Guid player, string scopeId, string itemId)
    {
        var normalizedScopeId = NormalizeScopeId(scopeId);
        if (normalizedScopeId == null)
            return false;

        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        var normalizedItemId = itemId.Trim();
        const int maxToggleAttempts = 4;

        for (var attempt = 0; attempt < maxToggleAttempts; attempt++)
        {
            await using var db = await GetDb();

            try
            {
                var existingLike = await db.DbContext.UiLikes.SingleOrDefaultAsync(like =>
                    like.ScopeId == normalizedScopeId &&
                    like.ItemId == normalizedItemId &&
                    like.PlayerUserId == player);

                if (existingLike != null)
                {
                    db.DbContext.UiLikes.Remove(existingLike);
                }
                else
                {
                    db.DbContext.UiLikes.Add(new UiLike
                    {
                        ScopeId = normalizedScopeId,
                        ItemId = normalizedItemId,
                        PlayerUserId = player,
                    });
                }

                await db.DbContext.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException) when (attempt < maxToggleAttempts - 1)
            {
                // Race on concurrent toggle (insert unique conflict / delete lost update), retry with fresh context.
            }
        }

        return false;
    }

    private static string? NormalizeScopeId(string scopeId)
    {
        if (string.IsNullOrWhiteSpace(scopeId))
            return null;

        return scopeId.Trim();
    }

    private static List<string> NormalizeUiLikeItemIds(IReadOnlyList<string> itemIds)
    {
        var unique = new HashSet<string>();
        var normalized = new List<string>(itemIds.Count);

        foreach (var itemId in itemIds)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            var trimmed = itemId.Trim();
            if (!unique.Add(trimmed))
                continue;

            normalized.Add(trimmed);
        }

        return normalized;
    }
}
