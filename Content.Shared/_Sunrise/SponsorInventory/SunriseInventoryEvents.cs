using System;
using System.Collections.Generic;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.SponsorInventory;

/// <summary>
/// Client request to update the selected sponsor pet.
/// </summary>
[Serializable, NetSerializable]
public sealed class SunriseInventoryPetSelectedEvent(string? selectedPetSelection) : EntityEventArgs
{
    public string? SelectedPetSelection = selectedPetSelection;
}

/// <summary>
/// Client request for the initial sponsor inventory snapshot.
/// </summary>
[Serializable, NetSerializable]
public sealed class SunriseInventoryInitialDataRequestEvent : EntityEventArgs
{
}

/// <summary>
/// Server response containing the sponsor inventory catalog, ownership, and saved profiles.
/// </summary>
[Serializable, NetSerializable]
public sealed class SunriseInventoryInitialDataEvent(
    SponsorInventoryConfig config,
    string catalogVersion,
    int sponsorTier,
    List<string> entitlements,
    List<string> ownedItemIds,
    Dictionary<int, SunriseInventoryProfile> profiles,
    int? balance,
    string revision) : EntityEventArgs
{
    public SponsorInventoryConfig Config = config;
    public string CatalogVersion = catalogVersion;
    public int SponsorTier = sponsorTier;
    public List<string> Entitlements = entitlements;
    public List<string> OwnedItemIds = ownedItemIds;
    public Dictionary<int, SunriseInventoryProfile> Profiles = profiles;
    public int? Balance = balance;
    public string Revision = revision;
}

/// <summary>
/// Client request to save one character slot's sponsor inventory profile.
/// </summary>
[Serializable, NetSerializable]
public sealed class SunriseInventoryProfileChangedEvent(int slot, SunriseInventoryProfile profile) : EntityEventArgs
{
    public int Slot = slot;
    public SunriseInventoryProfile Profile = profile;
}

/// <summary>
/// Server response with the persisted sponsor inventory profile.
/// </summary>
[Serializable, NetSerializable]
public sealed class SunriseInventoryProfileSyncedEvent(int slot, SunriseInventoryProfile profile, string revision) : EntityEventArgs
{
    public int Slot = slot;
    public SunriseInventoryProfile Profile = profile;
    public string Revision = revision;
}

/// <summary>
/// Client request to purchase exactly one sponsor inventory item or pack.
/// </summary>
[Serializable, NetSerializable]
public sealed class SunriseInventoryPurchaseRequestEvent(
    string? itemId,
    string? packId,
    string idempotencyKey) : EntityEventArgs
{
    public string? ItemId = itemId;
    public string? PackId = packId;
    public string IdempotencyKey = idempotencyKey;
}

/// <summary>
/// Server response for a sponsor inventory purchase attempt.
/// </summary>
[Serializable, NetSerializable]
public sealed class SunriseInventoryPurchaseResultEvent(SponsorInventoryPurchaseResult result) : EntityEventArgs
{
    public SponsorInventoryPurchaseResult Result = result;
}
