using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.PlayerCache;

[Serializable, NetSerializable]
public sealed class PlayerCacheData
{
    public string? GhostTheme { get; set; }
    public string? Pet { get; set; }
    public bool? EmoteVisibility { get; set; }
    public bool? LobbyTtsEnabled { get; set; }
    public bool? LobbyOthersTtsEnabled { get; set; }
    public bool? AdminChatTtsEnabled { get; set; }
}
