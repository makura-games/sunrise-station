using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Sunrise.SponsorInventory;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Sunrise.Interfaces.Shared;
using Robust.Client;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.SponsorInventory;

/// <summary>
/// Client-side cache and network bridge for sponsor inventory data used by lobby UI.
/// </summary>
public sealed partial class SunriseInventorySystem : EntitySystem
{
    [Dependency] private IBaseClient _baseClient = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly Dictionary<int, SunriseInventoryProfile> _profiles = new();
    private readonly HashSet<string> _ownedItemIds = new();
    private readonly HashSet<string> _entitlements = new();
    private SponsorInventoryConfig _config = new();
    private int _sponsorTier;
    private int? _balance;
    private string _revision = string.Empty;
    private bool _hasInitialData;
    private ISharedSponsorsManager? _sponsors;

    /// <summary>
    /// Raised after cached sponsor inventory data changes.
    /// </summary>
    public event Action? InventoryDataChanged;

    /// <summary>
    /// Raised after the server answers a sponsor inventory purchase request.
    /// </summary>
    public event Action<SponsorInventoryPurchaseResult>? InventoryPurchaseResultReceived;

    public override void Initialize()
    {
        base.Initialize();

        IoCManager.Instance!.TryResolveType(out _sponsors);
        SubscribeNetworkEvent<SunriseInventoryInitialDataEvent>(OnInitialData);
        SubscribeNetworkEvent<SunriseInventoryProfileSyncedEvent>(OnProfileSynced);
        SubscribeNetworkEvent<SunriseInventoryPurchaseResultEvent>(OnPurchaseResult);
        _baseClient.RunLevelChanged += OnRunLevelChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _baseClient.RunLevelChanged -= OnRunLevelChanged;
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs e)
    {
        if (e.NewLevel != ClientRunLevel.Initialize)
            return;

        _profiles.Clear();
        _ownedItemIds.Clear();
        _entitlements.Clear();
        _config = new SponsorInventoryConfig();
        _sponsorTier = 0;
        _balance = null;
        _revision = string.Empty;
        _hasInitialData = false;
        InventoryDataChanged?.Invoke();
    }

    private void OnInitialData(SunriseInventoryInitialDataEvent ev)
    {
        _config = ev.Config ?? new SponsorInventoryConfig();
        _config.Items ??= [];
        _config.Packs ??= [];

        if (string.IsNullOrWhiteSpace(_config.Version))
            _config.Version = ev.CatalogVersion ?? string.Empty;

        _sponsorTier = ev.SponsorTier;
        _balance = ev.Balance;
        _revision = ev.Revision ?? string.Empty;
        _hasInitialData = true;

        _entitlements.Clear();
        foreach (var entitlement in ev.Entitlements ?? [])
        {
            if (!string.IsNullOrWhiteSpace(entitlement))
                _entitlements.Add(entitlement);
        }

        _ownedItemIds.Clear();
        foreach (var item in ev.OwnedItemIds ?? [])
        {
            if (!string.IsNullOrWhiteSpace(item))
                _ownedItemIds.Add(item);
        }

        _profiles.Clear();
        foreach (var (slot, profile) in ev.Profiles ?? new Dictionary<int, SunriseInventoryProfile>())
        {
            if (profile != null)
                _profiles[slot] = profile.Clone();
        }

        InventoryDataChanged?.Invoke();
    }

    private void OnProfileSynced(SunriseInventoryProfileSyncedEvent ev)
    {
        ApplyInventoryProfile(ev.Slot, ev.Profile ?? new SunriseInventoryProfile());
        _revision = ev.Revision ?? string.Empty;
        InventoryDataChanged?.Invoke();
    }

    private void OnPurchaseResult(SunriseInventoryPurchaseResultEvent ev)
    {
        if (ev.Result == null)
            return;

        InventoryPurchaseResultReceived?.Invoke(ev.Result);

        if (!ev.Result.Success)
            return;

        _ownedItemIds.Clear();
        foreach (var item in ev.Result.OwnedItemIds ?? [])
        {
            if (!string.IsNullOrWhiteSpace(item))
                _ownedItemIds.Add(item);
        }

        _balance = ev.Result.Balance;
        _revision = ev.Result.Revision ?? string.Empty;
        InventoryDataChanged?.Invoke();
    }

    /// <summary>
    /// Returns a cloned sponsor inventory profile for the character slot.
    /// </summary>
    public SunriseInventoryProfile GetInventoryProfile(int slot)
    {
        return _profiles.TryGetValue(slot, out var profile)
            ? profile.Clone()
            : new SunriseInventoryProfile();
    }

    /// <summary>
    /// Validates, stores, and sends a sponsor inventory profile update for a character slot.
    /// </summary>
    public void SetInventoryProfile(int slot, SunriseInventoryProfile profile)
    {
        if (_player.LocalSession == null)
            return;

        var validProfile = SunriseInventoryValidation.EnsureValid(
            profile,
            _prototype,
            GetSponsorInventoryConfig(),
            GetPurchasedInventoryItems(),
            GetSponsorTier(),
            GetSponsorInventoryEntitlements());

        ApplyInventoryProfile(slot, validProfile);
        InventoryDataChanged?.Invoke();
        RaiseNetworkEvent(new SunriseInventoryProfileChangedEvent(slot, validProfile));
    }

    /// <summary>
    /// Requests the authoritative sponsor inventory snapshot from the server.
    /// </summary>
    public void RequestInitialData()
    {
        if (_player.LocalSession == null)
            return;

        RaiseNetworkEvent(new SunriseInventoryInitialDataRequestEvent());
    }

    /// <summary>
    /// Returns the latest known sponsor inventory catalog.
    /// </summary>
    public SponsorInventoryConfig GetSponsorInventoryConfig()
    {
        var config = _hasInitialData
            ? _config
            : _sponsors?.GetSponsorInventoryConfig() ?? new SponsorInventoryConfig();

        return CloneSponsorInventoryConfig(config);
    }

    /// <summary>
    /// Returns sponsor inventory item ids currently owned by the local player.
    /// </summary>
    public IReadOnlyCollection<string> GetPurchasedInventoryItems()
    {
        if (_hasInitialData)
            return _ownedItemIds.ToArray();

        return _sponsors?.GetClientPurchasedInventoryItems().ToArray() ?? [];
    }

    public int GetSponsorTier()
    {
        if (_hasInitialData)
            return _sponsorTier;

        return _sponsors?.GetClientSponsorInventoryInitialData().SponsorTier ?? 0;
    }

    public IReadOnlyCollection<string> GetSponsorInventoryEntitlements()
    {
        if (_hasInitialData)
            return _entitlements.ToArray();

        return _player.LocalSession == null
            ? []
            : _sponsors?.GetSponsorInventoryEntitlements(_player.LocalSession.UserId).ToArray() ?? [];
    }

    public int? GetBalance()
    {
        return _balance;
    }

    public string GetRevision()
    {
        return _revision;
    }

    /// <summary>
    /// Returns whether the local player may use the sponsor inventory item for the selected job.
    /// </summary>
    public bool CanUseItem(string itemId, string? jobId)
    {
        return SunriseInventoryValidation.CanUseItem(
            itemId,
            jobId,
            _prototype,
            GetSponsorInventoryConfig(),
            GetPurchasedInventoryItems(),
            GetSponsorTier(),
            GetSponsorInventoryEntitlements());
    }

    /// <summary>
    /// Sends a purchase request for one sponsor inventory item or pack.
    /// </summary>
    public void PurchaseInventoryItem(string? itemId, string? packId)
    {
        if (_player.LocalSession == null)
            return;

        RaiseNetworkEvent(new SunriseInventoryPurchaseRequestEvent(
            itemId,
            packId,
            Guid.NewGuid().ToString("N")));
    }

    /// <summary>
    /// Saves the local pet selection CVar and mirrors the change to the server cache.
    /// </summary>
    public void SelectPet(string? petSelection)
    {
        _cfg.SetCVar(SunriseCCVars.SponsorPet, petSelection ?? string.Empty);
        RaiseNetworkEvent(new SunriseInventoryPetSelectedEvent(petSelection));
    }

    private void ApplyInventoryProfile(int slot, SunriseInventoryProfile profile)
    {
        if (profile.IsEmpty())
            _profiles.Remove(slot);
        else
            _profiles[slot] = profile.Clone();
    }

    private static SponsorInventoryConfig CloneSponsorInventoryConfig(SponsorInventoryConfig config)
    {
        return new SponsorInventoryConfig
        {
            Version = config.Version,
            Items = (config.Items ?? [])
                .Where(item => item != null)
                .Select(item => new SponsorInventoryItemInfo
                {
                    Id = item.Id,
                    EntityPrototype = item.EntityPrototype,
                    Usage = new SponsorInventoryUsageInfo
                    {
                        Jobs = item.Usage?.Jobs?.ToArray() ?? [],
                        Departments = item.Usage?.Departments?.ToArray() ?? [],
                        ExcludeJobs = item.Usage?.ExcludeJobs?.ToArray() ?? [],
                    },
                    Access = new SponsorInventoryAccessInfo
                    {
                        Tier = item.Access.Tier == null
                            ? null
                            : new SponsorInventoryTierAccessInfo
                            {
                                Value = item.Access.Tier.Value,
                                Inherit = item.Access.Tier.Inherit,
                            },
                        Entitlements = item.Access.Entitlements.ToArray(),
                    },
                    Purchasable = item.Purchasable,
                    Price = item.Price,
                })
                .ToArray(),
            Packs = (config.Packs ?? [])
                .Where(pack => pack != null)
                .Select(pack => new SponsorInventoryPackInfo
                {
                    Id = pack.Id,
                    Name = pack.Name,
                    Description = pack.Description,
                    PreviewEntityPrototype = pack.PreviewEntityPrototype,
                    InventoryItemIds = pack.InventoryItemIds?.ToArray() ?? [],
                    Price = pack.Price,
                })
                .ToArray(),
        };
    }
}
