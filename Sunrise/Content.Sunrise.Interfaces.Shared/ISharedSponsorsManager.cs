using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Sunrise.Interfaces.Shared;

public interface ISharedSponsorsManager
{
    public void Initialize();

    public event Action? LoadedSponsorInfo;
    public event Action<List<SponsorInfo>>? LoadedSponsorTiers;
    public event Action<SponsorInventoryConfig>? LoadedSponsorInventoryConfig
    {
        add { }
        remove { }
    }
    public event Action<NetUserId, SponsorInventoryInitialData>? LoadedSponsorInventoryInitialData
    {
        add { }
        remove { }
    }

    // Client
    public List<string> GetClientPrototypes();
    public bool ClientAllowedRespawn();
    public bool ClientAllowedFlavor();
    public int ClientGetSizeFlavor();

    public bool ClientIsSponsor();
    public List<SponsorInfo> GetSponsorTiers();
    public string GetSponsorProjectName() => string.Empty;
    public SponsorInventoryConfig GetSponsorInventoryConfig() => new();
    public List<string> GetClientPurchasedInventoryItems() => [];
    public SponsorInventoryInitialData GetClientSponsorInventoryInitialData() => new();
    public IReadOnlyCollection<string> GetSponsorInventoryEntitlements(NetUserId userId) => [];

    // Server
    public bool TryGetPrototypes(NetUserId userId, [NotNullWhen(true)] out List<string>? prototypes);
    public bool TryGetOocTitle(NetUserId userId, [NotNullWhen(true)] out string? title);
    public bool TryGetOocColor(NetUserId userId, [NotNullWhen(true)] out Color? color);
    public bool TryGetSpawnEquipment(NetUserId userId, [NotNullWhen(true)] out string? spawnEquipment);
    public bool TryGetGhostThemes(NetUserId userId, [NotNullWhen(true)] out List<string>? ghostTheme);
    public bool TryGetBypassRoles(NetUserId userId, [NotNullWhen(true)] out List<string>? bypassRoles);
    public int GetSizeFlavor(NetUserId userId);
    public bool IsAllowedFlavor(NetUserId userId);
    public int GetExtraCharSlots(NetUserId userId);
    public bool HavePriorityJoin(NetUserId userId);
    public bool IsSponsor(NetUserId userId);
    public bool IsAllowedRespawn(NetUserId userId);
    public List<ICommonSession> PickPrioritySessions(List<ICommonSession> sessions, string roleId);
    public NetUserId? PickRoleSession(HashSet<NetUserId> users, string roleId);
    public bool TryGetPriorityGhostRoles(NetUserId userId, [NotNullWhen(true)] out List<string>? priorityAntags);
    public bool TryGetPriorityAntags(NetUserId userId, [NotNullWhen(true)] out List<string>? priorityAntags);
    public bool TryGetPriorityRoles(NetUserId userId, [NotNullWhen(true)] out List<string>? priorityRoles);
    public bool TryGetPets(NetUserId userId, [NotNullWhen(true)] out List<string>? petSelections);
    public int GetSponsorTier(NetUserId userId) => 0;

    public bool TryGetPurchasedInventoryItems(NetUserId userId, [NotNullWhen(true)] out List<string>? inventoryItems)
    {
        inventoryItems = null;
        return false;
    }

    /// <summary>
    /// Loads catalog, ownership, profile, and revision data for the sponsor inventory UI.
    /// </summary>
    public ValueTask<SponsorInventoryInitialData> GetSponsorInventoryInitialDataAsync(
        NetUserId userId,
        CancellationToken cancel = default)
    {
        var config = GetSponsorInventoryConfig();
        var ownedItemIds = TryGetPurchasedInventoryItems(userId, out var items) && items != null
            ? items.ToArray()
            : Array.Empty<string>();

        return ValueTask.FromResult(new SponsorInventoryInitialData
        {
            CatalogVersion = config.Version,
            SponsorTier = GetSponsorTier(userId),
            Entitlements = GetSponsorInventoryEntitlements(userId).ToArray(),
            OwnedItemIds = ownedItemIds,
        });
    }

    /// <summary>
    /// Loads a saved sponsor inventory profile for a character slot.
    /// </summary>
    public ValueTask<SponsorInventoryProfileInfo?> GetSponsorInventoryProfileAsync(
        NetUserId userId,
        int slot,
        CancellationToken cancel = default)
    {
        return ValueTask.FromResult<SponsorInventoryProfileInfo?>(null);
    }

    /// <summary>
    /// Persists a server-validated sponsor inventory profile for a character slot.
    /// </summary>
    public ValueTask<SponsorInventoryProfileSaveResult> SaveSponsorInventoryProfileAsync(
        NetUserId userId,
        int slot,
        SponsorInventoryProfileInfo profile,
        CancellationToken cancel = default)
    {
        return ValueTask.FromResult(new SponsorInventoryProfileSaveResult
        {
            Profile = profile,
        });
    }

    /// <summary>
    /// Purchases one sponsor inventory item or pack through the backing sponsor service.
    /// </summary>
    public ValueTask<SponsorInventoryPurchaseResult> PurchaseSponsorInventoryItemAsync(
        NetUserId userId,
        SponsorInventoryPurchaseRequest request,
        CancellationToken cancel = default)
    {
        return ValueTask.FromResult(new SponsorInventoryPurchaseResult
        {
            Success = false,
            Error = "not-implemented",
        });
    }

    public void Update();
}

/// <summary>
/// Sponsor inventory catalog sent to clients and used by server validation.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventoryConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public SponsorInventoryItemInfo[] Items { get; set; } = [];

    [JsonPropertyName("packs")]
    public SponsorInventoryPackInfo[] Packs { get; set; } = [];
}

/// <summary>
/// Условие временной выдачи предмета по спонсорскому тиру без покупки.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventoryTierAccessInfo
{
    /// <summary>
    /// Требуемый спонсорский тир.
    /// </summary>
    [JsonPropertyName("value")]
    public int Value { get; set; }

    /// <summary>
    /// Получают ли временный доступ более высокие тиры.
    /// </summary>
    [JsonPropertyName("inherit")]
    public bool Inherit { get; set; } = true;
}

/// <summary>
/// Альтернативные способы временно использовать предмет спонсорского инвентаря без владения.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventoryAccessInfo
{
    /// <summary>
    /// Условие временного доступа по спонсорскому тиру или null, если тир не выдаёт предмет.
    /// </summary>
    [JsonPropertyName("tier")]
    public SponsorInventoryTierAccessInfo? Tier { get; set; }

    /// <summary>
    /// Набор прав, которые должны присутствовать одновременно.
    /// </summary>
    [JsonPropertyName("entitlements")]
    public string[] Entitlements { get; set; } = [];
}

/// <summary>
/// Ограничения использования предмета спонсорского инвентаря выбранной профессией.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventoryUsageInfo
{
    /// <summary>
    /// Профессии, которым разрешено использовать предмет.
    /// Объединяются с <see cref="Departments"/> по OR.
    /// </summary>
    [JsonPropertyName("jobs")]
    public string[] Jobs { get; set; } = [];

    /// <summary>
    /// Отделы, профессиям которых разрешено использовать предмет.
    /// Если профессии и отделы не заданы, предмет разрешён всем профессиям вне исключений.
    /// </summary>
    [JsonPropertyName("departments")]
    public string[] Departments { get; set; } = [];

    /// <summary>
    /// Профессии, которым запрещено использовать предмет независимо от разрешающих условий.
    /// </summary>
    [JsonPropertyName("excludeJobs")]
    public string[] ExcludeJobs { get; set; } = [];
}

/// <summary>
/// Элемент каталога спонсорского инвентаря.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventoryItemInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("entityPrototype")]
    public string EntityPrototype { get; set; } = string.Empty;

    /// <summary>
    /// Ограничения использования предмета выбранной профессией. Не влияют на покупку.
    /// </summary>
    [JsonPropertyName("usage")]
    public SponsorInventoryUsageInfo Usage { get; set; } = new();

    /// <summary>
    /// Условия временного доступа без владения: достаточный тир или одновременно все перечисленные права.
    /// </summary>
    [JsonPropertyName("access")]
    public SponsorInventoryAccessInfo Access { get; set; } = new();

    /// <summary>
    /// Можно ли купить предмет и отображать его в спонсорском магазине. Не зависит от <see cref="Access"/>.
    /// </summary>
    [JsonPropertyName("purchasable")]
    public bool Purchasable { get; set; } = true;

    [JsonPropertyName("price")]
    public int Price { get; set; }
}

/// <summary>
/// Catalog entry for a purchasable sponsor inventory pack.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventoryPackInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Optional display name supplied by the external sponsor service.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Optional display description supplied by the external sponsor service.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Optional entity prototype used to preview the pack in the UI.
    /// </summary>
    [JsonPropertyName("previewEntityPrototype")]
    public string? PreviewEntityPrototype { get; set; }

    [JsonPropertyName("inventoryItemIds")]
    public string[] InventoryItemIds { get; set; } = [];

    [JsonPropertyName("price")]
    public int Price { get; set; }
}

/// <summary>
/// Initial sponsor inventory snapshot for one player session.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventoryInitialData
{
    [JsonPropertyName("catalogVersion")]
    public string CatalogVersion { get; set; } = string.Empty;

    [JsonPropertyName("sponsorTier")]
    public int SponsorTier { get; set; }

    [JsonPropertyName("entitlements")]
    public string[] Entitlements { get; set; } = [];

    [JsonPropertyName("ownedItemIds")]
    public string[] OwnedItemIds { get; set; } = [];

    [JsonPropertyName("profiles")]
    public Dictionary<int, SponsorInventoryProfileInfo> Profiles { get; set; } = new();

    /// <summary>
    /// Optional sponsor inventory balance when the backing service exposes one.
    /// </summary>
    [JsonPropertyName("balance")]
    public int? Balance { get; set; }

    [JsonPropertyName("revision")]
    public string Revision { get; set; } = string.Empty;
}

/// <summary>
/// Flexible response shape accepted from the external sponsor inventory initial-data endpoint.
/// </summary>
[Serializable]
public sealed class SponsorInventoryInitialDataApiResponse
{
    /// <summary>
    /// Complete catalog object, or null when the API returns catalog parts at the top level.
    /// </summary>
    [JsonPropertyName("config")]
    public SponsorInventoryConfig? Config { get; set; }

    /// <summary>
    /// Legacy catalog version fallback used when catalogVersion is absent.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// Catalog version returned by the external sponsor service.
    /// </summary>
    [JsonPropertyName("catalogVersion")]
    public string? CatalogVersion { get; set; }

    /// <summary>
    /// Top-level catalog items used when config is absent.
    /// </summary>
    [JsonPropertyName("items")]
    public SponsorInventoryItemInfo[]? Items { get; set; }

    /// <summary>
    /// Top-level catalog packs used when config is absent.
    /// </summary>
    [JsonPropertyName("packs")]
    public SponsorInventoryPackInfo[]? Packs { get; set; }

    /// <summary>
    /// Sponsor tier returned by the inventory endpoint, or null to use the fallback sponsor tier.
    /// </summary>
    [JsonPropertyName("sponsorTier")]
    public int? SponsorTier { get; set; }

    /// <summary>
    /// Итоговые спонсорские права, полученные от внешнего сервиса.
    /// </summary>
    [JsonPropertyName("entitlements")]
    public string[]? Entitlements { get; set; }

    /// <summary>
    /// Owned sponsor inventory item IDs, or null when the service returns no ownership data.
    /// </summary>
    [JsonPropertyName("ownedItemIds")]
    public string[]? OwnedItemIds { get; set; }

    /// <summary>
    /// Saved profiles keyed by character slot, or null when no profiles are returned.
    /// </summary>
    [JsonPropertyName("profiles")]
    public Dictionary<int, SponsorInventoryProfileInfo>? Profiles { get; set; }

    /// <summary>
    /// Optional sponsor inventory balance returned by the backing service.
    /// </summary>
    [JsonPropertyName("balance")]
    public int? Balance { get; set; }

    /// <summary>
    /// Revision token returned by the backing service, or null when the response is not revisioned.
    /// </summary>
    [JsonPropertyName("revision")]
    public string? Revision { get; set; }

    public SponsorInventoryConfig ToConfig()
    {
        var config = Config ?? new SponsorInventoryConfig
        {
            Items = Items ?? [],
            Packs = Packs ?? [],
        };

        if (string.IsNullOrWhiteSpace(config.Version))
            config.Version = !string.IsNullOrWhiteSpace(CatalogVersion)
                ? CatalogVersion
                : Version ?? string.Empty;

        config.Items ??= [];
        config.Packs ??= [];
        return config;
    }

    public SponsorInventoryInitialData ToInfo(SponsorInventoryConfig config, int fallbackSponsorTier)
    {
        return new SponsorInventoryInitialData
        {
            CatalogVersion = !string.IsNullOrWhiteSpace(CatalogVersion)
                ? CatalogVersion
                : config.Version,
            SponsorTier = SponsorTier ?? fallbackSponsorTier,
            Entitlements = Entitlements ?? [],
            OwnedItemIds = OwnedItemIds ?? [],
            Profiles = Profiles ?? new Dictionary<int, SponsorInventoryProfileInfo>(),
            Balance = Balance,
            Revision = Revision ?? string.Empty,
        };
    }
}

/// <summary>
/// External sponsor inventory profile DTO.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventoryProfileInfo
{
    [JsonPropertyName("global")]
    public SponsorInventorySelectionInfo Global { get; set; } = new();

    [JsonPropertyName("jobs")]
    public Dictionary<string, SponsorInventorySelectionInfo> Jobs { get; set; } = new();
}

/// <summary>
/// External sponsor inventory selection DTO for one global or job-specific layer.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventorySelectionInfo
{
    [JsonPropertyName("slotItems")]
    public Dictionary<string, string> SlotItems { get; set; } = new();

    [JsonPropertyName("bagItems")]
    public List<string> BagItems { get; set; } = [];
}

/// <summary>
/// Purchase request for exactly one sponsor inventory item or pack.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventoryPurchaseRequest
{
    /// <summary>
    /// Item ID to purchase, or null when the request purchases a pack.
    /// </summary>
    [JsonPropertyName("itemId")]
    public string? ItemId { get; set; }

    /// <summary>
    /// Pack ID to purchase, or null when the request purchases a single item.
    /// </summary>
    [JsonPropertyName("packId")]
    public string? PackId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; set; } = string.Empty;
}

/// <summary>
/// Result returned after saving a sponsor inventory profile.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventoryProfileSaveResult
{
    [JsonPropertyName("profile")]
    public SponsorInventoryProfileInfo Profile { get; set; } = new();

    [JsonPropertyName("revision")]
    public string Revision { get; set; } = string.Empty;
}

/// <summary>
/// Result returned after a sponsor inventory purchase attempt.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorInventoryPurchaseResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("ownedItemIds")]
    public string[] OwnedItemIds { get; set; } = [];

    /// <summary>
    /// Updated sponsor inventory balance, or null when the backing service does not expose one.
    /// </summary>
    [JsonPropertyName("balance")]
    public int? Balance { get; set; }

    [JsonPropertyName("revision")]
    public string Revision { get; set; } = string.Empty;

    /// <summary>
    /// Failure reason from the backing sponsor service, or null when the purchase succeeds.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

[Serializable, NetSerializable]
public sealed class SponsorInfo
{
    [JsonPropertyName("tier")]
    public int Tier { get; set; }

    /// <summary>
    /// Optional out-of-character title granted by the sponsor tier.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Optional out-of-character chat color granted by the sponsor tier.
    /// </summary>
    [JsonPropertyName("oocColor")]
    public string? OOCColor { get; set; }

    [JsonPropertyName("priorityJoin")]
    public bool HavePriorityJoin { get; set; } = false;

    [JsonPropertyName("extraSlots")]
    public int ExtraSlots { get; set; }

    [JsonPropertyName("allowedRespawn")]
    public bool AllowedRespawn { get; set; } = false;

    [JsonPropertyName("allowedFlavor")]
    public bool AllowedFlavor { get; set; } = false;

    [JsonPropertyName("sizeFlavor")]
    public int SizeFlavor { get; set; }

    [JsonPropertyName("isTechnical")]
    public bool IsTechnical { get; set; } = false;

    [JsonPropertyName("ghostThemes")]
    public string[] GhostThemes { get; set; } = [];

    [JsonPropertyName("pets")]
    public string[] Pets { get; set; } = [];

    /// <summary>
    /// Optional starting equipment prototype granted by the sponsor tier.
    /// </summary>
    [JsonPropertyName("spawnEquipment")]
    public string? SpawnEquipment { get; set; }

    [JsonPropertyName("allowedMarkings")]
    public string[] AllowedMarkings { get; set; } = [];

    [JsonPropertyName("allowedVoices")]
    public string[] AllowedVoices { get; set; } = [];

    [JsonPropertyName("allowedSpecies")]
    public string[] AllowedSpecies { get; set; } = [];

    [JsonPropertyName("openAntags")]
    public string[] OpenAntags { get; set; } = [];

    [JsonPropertyName("openRoles")]
    public string[] OpenRoles { get; set; } = [];

    [JsonPropertyName("openGhostRoles")]
    public string[] OpenGhostRoles { get; set; } = [];

    [JsonPropertyName("priorityAntags")]
    public string[] PriorityAntags { get; set; } = [];

    [JsonPropertyName("priorityRoles")]
    public string[] PriorityRoles { get; set; } = [];

    [JsonPropertyName("priorityGhostRoles")]
    public string[] PriorityGhostRoles { get; set; } = [];

    [JsonPropertyName("BypassRoles")]
    public string[] BypassRoles { get; set; } = [];
}
