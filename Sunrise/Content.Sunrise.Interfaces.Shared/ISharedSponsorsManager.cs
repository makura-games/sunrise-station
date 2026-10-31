using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
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

    // Client
    public List<string> GetClientPrototypes();
    public bool ClientAllowedRespawn();
    public bool ClientAllowedFlavor();
    public int ClientGetSizeFlavor();
    /// <summary>
    /// Разрешен ли лобби-TTS для текущего клиента.
    /// </summary>
    public bool ClientAllowedLobbyTts();

    /// <summary>
    /// Возвращает список OOC-титулов, разрешенных текущему клиенту.
    /// </summary>
    public List<string> GetAllowedOocTitles();

    /// <summary>
    /// Возвращает список OOC-цветов, разрешенных текущему клиенту.
    /// </summary>
    public List<string> GetAllowedOocColors();

    /// <summary>
    /// Возвращает список OOC-градиентов, разрешенных текущему клиенту.
    /// </summary>
    public List<string> GetAllowedOocGradients();

    public bool ClientIsSponsor();
    public int ClientGetTier();
    public string? ClientGetTierTitle();
    public Color? ClientGetTierColor();
    public string? ClientGetTierColorHex();
    public List<SponsorInfo> GetSponsorTiers();

    // Server
    public bool TryGetPrototypes(NetUserId userId, [NotNullWhen(true)] out List<string>? prototypes);
    public bool TryGetOocTitle(NetUserId userId, [NotNullWhen(true)] out string? title);
    public bool TryGetOocColor(NetUserId userId, [NotNullWhen(true)] out Color? color);
    public bool TryGetSpawnEquipment(NetUserId userId, [NotNullWhen(true)] out string? spawnEquipment);
    public bool TryGetGhostThemes(NetUserId userId, [NotNullWhen(true)] out List<string>? ghostTheme);
    public bool TryGetBypassRoles(NetUserId userId, [NotNullWhen(true)] out List<string>? bypassRoles);
    public int GetSizeFlavor(NetUserId userId);
    public bool IsAllowedFlavor(NetUserId userId);
    public bool IsAllowedLobbyTts(NetUserId userId);
    public bool IsAllowedOocTitleEmoji(NetUserId userId);
    public bool TryGetAllowedOocGradients(NetUserId userId, [NotNullWhen(true)] out List<string>? gradients);
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
    public void Update();
}

[Serializable, NetSerializable]
public sealed class SponsorInfo
{
    [JsonPropertyName("tier")]
    public int Tier { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

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

    [JsonPropertyName("ghostThemes")]
    public string[] GhostThemes { get; set; } = [];

    [JsonPropertyName("pets")]
    public string[] Pets { get; set; } = [];

    [JsonPropertyName("spawnEquipment")]
    public string? SpawnEquipment { get; set; }

    [JsonPropertyName("allowedMarkings")]
    public string[] AllowedMarkings { get; set; } = [];

    [JsonPropertyName("allowedVoices")]
    public string[] AllowedVoices { get; set; } = [];

    /// <summary>
    /// Разрешен ли лобби-TTS для данного тира спонсора.
    /// </summary>
    [JsonPropertyName("allowedLobbyTts")]
    public bool AllowedLobbyTts { get; set; } = false;

    /// <summary>
    /// Список разрешенных OOC-титулов для данного тира спонсора.
    /// </summary>
    [JsonPropertyName("allowedOocTitles")]
    public string[] AllowedOocTitles { get; set; } = [];

    /// <summary>
    /// Список разрешенных OOC-цветов для данного тира спонсора.
    /// </summary>
    [JsonPropertyName("allowedOocColors")]
    public string[] AllowedOocColors { get; set; } = [];

    [JsonPropertyName("allowedOocTitleEmoji")]
    public bool AllowedOocTitleEmoji { get; set; } = false;

    [JsonPropertyName("allowedLoadouts")]
    public string[] AllowedLoadouts { get; set; } = [];

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
