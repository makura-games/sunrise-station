using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Shared._Sunrise.PlayerCache;
using Content.Shared._Sunrise.SponsorInventory;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.SponsorInventory;

public sealed partial class SunriseInventorySystem
{
    /*
     * Loads, persists, and caches selected sponsor inventory profiles.
     */

    /// <summary>
    /// Validates and persists a sponsor inventory profile for one character slot.
    /// </summary>
    public async Task<bool> TrySetInventoryProfileAsync(ICommonSession session, int slot, SunriseInventoryProfile profile)
    {
        if (_sponsors == null || profile == null || !CanSetInventoryProfile(session, slot))
            return false;

        await SetInventoryProfile(session, slot, profile);
        return true;
    }

    /// <summary>
    /// Saves the selected sponsor pet if the player is allowed to use it.
    /// </summary>
    public bool TrySetPetSelection(NetUserId userId, string? petSelection)
    {
        if (!CanSetPetSelection(userId, petSelection))
            return false;

        SetPetSelection(userId, petSelection);
        return true;
    }

    private bool CanSetPetSelection(NetUserId userId, string? petSelection)
    {
        return string.IsNullOrEmpty(petSelection) ||
               petSelection.Length <= MaxPetSelectionIdLength &&
               _validation.ValidatePetSelection(petSelection, userId);
    }

    private void SetPetSelection(NetUserId userId, string? petSelection)
    {
        if (!_playerCache.TryGetCache(userId, out var cache))
            cache = new PlayerCacheData();

        cache.Pet = petSelection ?? string.Empty;
        _playerCache.SetCache(userId, cache);
    }

    private bool CanSetInventoryProfile(ICommonSession session, int slot)
    {
        return slot >= 0 &&
               _preferences.TryGetCachedPreferences(session.UserId, out var preferences) &&
               preferences.Characters.ContainsKey(slot);
    }

    private async Task SendInitialData(
        ICommonSession session,
        SponsorInventoryInitialData? cachedInitialData = null)
    {
        var initialData = cachedInitialData ?? (_sponsors != null
            ? await _sponsors.GetSponsorInventoryInitialDataAsync(session.UserId)
            : new SponsorInventoryInitialData());
        initialData ??= new SponsorInventoryInitialData();

        var profiles = new Dictionary<int, SunriseInventoryProfile>();
        if (_preferences.TryGetCachedPreferences(session.UserId, out var preferences))
        {
            var profileTasks = new Dictionary<int, Task<SunriseInventoryProfile>>(preferences.Characters.Count);
            foreach (var slot in preferences.Characters.Keys)
            {
                profileTasks[slot] = GetInventoryProfileAsync(session.UserId, slot, initialData).AsTask();
            }

            await Task.WhenAll(profileTasks.Values);
            foreach (var (slot, profileTask) in profileTasks)
            {
                var profile = await profileTask;
                var validProfile = _sponsors != null
                    ? SunriseInventoryValidation.EnsureValid(profile, session, _prototype, _sponsors)
                    : new SunriseInventoryProfile();
                validProfile = EnsureValidForCharacter(session, slot, validProfile);

                if (!validProfile.IsEmpty())
                    profiles[slot] = validProfile;

                SetInventoryProfileCache(session.UserId, slot, validProfile);
            }
        }

        var config = _sponsors?.GetSponsorInventoryConfig() ?? new SponsorInventoryConfig();
        config.Items ??= [];
        config.Packs ??= [];
        var catalogVersion = string.IsNullOrWhiteSpace(initialData.CatalogVersion)
            ? config.Version ?? string.Empty
            : initialData.CatalogVersion;

        var ev = new SunriseInventoryInitialDataEvent(
            config,
            catalogVersion,
            initialData.SponsorTier,
            (initialData.Entitlements ?? []).ToList(),
            (initialData.OwnedItemIds ?? []).ToList(),
            profiles,
            initialData.Balance,
            initialData.Revision ?? string.Empty);

        RaiseNetworkEvent(ev, session);
    }

    private async Task SetInventoryProfile(ICommonSession session, int slot, SunriseInventoryProfile profile)
    {
        if (_sponsors == null)
            return;

        var validProfile = SunriseInventoryValidation.EnsureValid(
            profile,
            session,
            _prototype,
            _sponsors);
        validProfile = EnsureValidForCharacter(session, slot, validProfile);

        var saved = await _sponsors.SaveSponsorInventoryProfileAsync(session.UserId, slot, validProfile.ToInfo());
        var savedProfile = SunriseInventoryProfile.FromInfo(saved.Profile);
        var syncedProfile = SunriseInventoryValidation.EnsureValid(
            savedProfile,
            session,
            _prototype,
            _sponsors);
        syncedProfile = EnsureValidForCharacter(session, slot, syncedProfile);

        SetInventoryProfileCache(session.UserId, slot, syncedProfile);
        RaiseNetworkEvent(new SunriseInventoryProfileSyncedEvent(slot, syncedProfile, saved.Revision), session);
    }

    private async ValueTask<SunriseInventoryProfile> GetSelectedInventoryProfileAsync(NetUserId userId)
    {
        var preferences = _preferences.GetPreferencesOrNull(userId);
        if (preferences == null)
            return new SunriseInventoryProfile();

        return await GetInventoryProfileAsync(userId, preferences.SelectedCharacterIndex);
    }

    private async ValueTask<SunriseInventoryProfile> GetInventoryProfileAsync(
        NetUserId userId,
        int slot,
        SponsorInventoryInitialData? initialData = null)
    {
        if (initialData?.Profiles != null &&
            initialData.Profiles.TryGetValue(slot, out var initialDataProfile))
        {
            return SunriseInventoryProfile.FromInfo(initialDataProfile);
        }

        if (_profiles.TryGetValue(userId, out var profiles) &&
            profiles.TryGetValue(slot, out var profile))
            return profile.Clone();

        if (_sponsors == null)
            return new SunriseInventoryProfile();

        return SunriseInventoryProfile.FromInfo(await _sponsors.GetSponsorInventoryProfileAsync(userId, slot));
    }

    private void SetInventoryProfileCache(NetUserId userId, int slot, SunriseInventoryProfile profile)
    {
        if (!_profiles.TryGetValue(userId, out var profiles))
        {
            profiles = new Dictionary<int, SunriseInventoryProfile>();
            _profiles[userId] = profiles;
        }

        if (profile.IsEmpty())
            profiles.Remove(slot);
        else
            profiles[slot] = profile.Clone();

        if (profiles.Count == 0)
            _profiles.Remove(userId);
    }
}
