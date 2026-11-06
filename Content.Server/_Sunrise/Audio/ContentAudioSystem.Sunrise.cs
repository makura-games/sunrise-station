using Content.Server._Sunrise.GameTicking.Events;
using Content.Shared.Audio.Events;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Audio;

public sealed partial class ContentAudioSystem
{
    private void InitializeSunriseRoundFlowAudio()
    {
        SubscribeLocalEvent<RoundLobbyReadyEvent>(OnRoundLobbyReady);
    }

    private void HandleSunriseRoundEndLobbyPlaylist()
    {
        _lobbyPlaylist = null;
    }

    private void OnRoundLobbyReady(ref RoundLobbyReadyEvent ev)
    {
        _lobbyPlaylist = ShuffleLobbyPlaylist();
        RaiseNetworkEvent(new LobbyPlaylistChangedEvent(_lobbyPlaylist));
    }
}
