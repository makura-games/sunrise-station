#pragma warning disable IDE0130

namespace Content.Client.GameTicking.Managers;

public sealed partial class ClientGameTicker
{
    /// <summary>
    /// Test-only hook for integration tests that need deterministic lobby fallback values.
    /// </summary>
    internal void SetTestFallbacks(
        bool hasLobbyStatus,
        string? lobbyType = null,
        string? lobbyParallax = null,
        string? lobbyAnimation = null,
        string? lobbyArt = null)
    {
        HasLobbyStatus = hasLobbyStatus;

        if (lobbyType != null)
            LobbyType = lobbyType;

        if (lobbyParallax != null)
            LobbyParallax = lobbyParallax;

        if (lobbyAnimation != null)
            LobbyAnimation = lobbyAnimation;

        if (lobbyArt != null)
            LobbyArt = lobbyArt;
    }
}
