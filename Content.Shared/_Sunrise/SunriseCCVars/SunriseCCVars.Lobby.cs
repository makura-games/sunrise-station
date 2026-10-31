using Content.Shared._Sunrise.Lobby;
using Robust.Shared.Configuration;

namespace Content.Shared._Sunrise.SunriseCCVars;

public sealed partial class SunriseCCVars
{
    /// <summary>
    /// ID прототипа набора фонов лобби.
    /// All означает, что будут использованы все доступные фоны.
    /// </summary>
    public static readonly CVarDef<string> LobbyBackgroundPreset =
        CVarDef.Create("lobby.background_preset", "AllAllAll", CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Определяет, какой тип фона лобби будет использован.
    /// Random, Art, Animation или Parallax.
    /// </summary>
    /// <seealso cref="LobbyArtPrototype"/>
    /// <seealso cref="LobbyAnimationPrototype"/>
    /// <seealso cref="LobbyParallaxPrototype"/>
    public static readonly CVarDef<string> LobbyBackgroundType =
        CVarDef.Create("lobby.background", "Random", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Определяет, какой лобби-арт будет использован. Позволяет игроку выбрать один арт, который будет использован постоянно.
    /// Random означает, что будет выбираться случайно из списка.
    /// </summary>
    /// <seealso cref="LobbyArtPrototype"/>
    public static readonly CVarDef<string> LobbyArt =
        CVarDef.Create("lobby.art", "Random", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Определяет, какая лобби-анимация будет использована. Позволяет игроку выбрать одну анимацию, который будет использован постоянно.
    /// Random означает, что будет выбираться случайно из списка.
    /// </summary>
    /// <seealso cref="LobbyAnimationPrototype"/>
    public static readonly CVarDef<string> LobbyAnimation =
        CVarDef.Create("lobby.animation", "Random", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Определяет, какой лобби-параллакс будет использован. Позволяет игроку выбрать один параллакс, который будет использован постоянно.
    /// Random означает, что будет выбираться случайно из списка.
    /// </summary>
    /// <seealso cref="LobbyParallaxPrototype"/>
    public static readonly CVarDef<string> LobbyParallax =
        CVarDef.Create("lobby.parallax", "Random", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Выгружать ли ресурсы лобби из видеопамяти при смене фона или входе в раунд.
    /// </summary>
    public static readonly CVarDef<bool> LobbyUnloadResources =
        CVarDef.Create("lobby.unload_resources", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Прозрачность интерфейса лобби (0.0-1.0)
    /// </summary>
    public static readonly CVarDef<float> LobbyOpacity =
        CVarDef.Create("lobby.lobby_opacity", 0.90f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the servers hub section in the lobby is collapsed.
    /// </summary>
    public static readonly CVarDef<bool> LobbyCollapsedServersHub =
        CVarDef.Create("lobby.collapsed_servers_hub", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the contributors section in the lobby is collapsed.
    /// </summary>
    public static readonly CVarDef<bool> LobbyCollapsedContributors =
        CVarDef.Create("lobby.collapsed_contributors", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the changelog section in the lobby is collapsed.
    /// </summary>
    public static readonly CVarDef<bool> LobbyCollapsedChangelog =
        CVarDef.Create("lobby.collapsed_changelog", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the server info section in the lobby is collapsed.
    /// </summary>
    public static readonly CVarDef<bool> LobbyCollapsedServerInfo =
        CVarDef.Create("lobby.collapsed_server_info", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the character setup section in the lobby is collapsed.
    /// </summary>
    public static readonly CVarDef<bool> LobbyCollapsedCharacterInfo =
        CVarDef.Create("lobby.collapsed_character_info", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the lobby chat section is collapsed.
    /// </summary>
    public static readonly CVarDef<bool> LobbyCollapsedChat =
        CVarDef.Create("lobby.collapsed_chat", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the Makura ID binding section is collapsed.
    /// </summary>
    public static readonly CVarDef<bool> LobbyCollapsedMakuraId =
        CVarDef.Create("lobby.collapsed_makura_id", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the sponsor section in the lobby is collapsed.
    /// </summary>
    public static readonly CVarDef<bool> LobbyCollapsedSponsor =
        CVarDef.Create("lobby.collapsed_sponsor", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the playtime top section in the lobby is collapsed.
    /// </summary>
    public static readonly CVarDef<bool> LobbyCollapsedPlaytimeTop =
        CVarDef.Create("lobby.collapsed_playtime_top", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
