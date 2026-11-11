using Content.Client.Lobby;
using Content.Shared._Sunrise.Audio.Events;

// ReSharper disable once CheckNamespace
namespace Content.Client.Audio;

public sealed partial class ContentAudioSystem
{
    private void InitializeSunriseLobbyMusic()
    {
        SubscribeNetworkEvent<RoundEndMusicEvent>(OnRoundEndMusic);
    }

    private void CacheSunriseLobbyPlaylist(string[] playlist)
    {
        _lobbyPlaylist = playlist;
    }

    private bool ShouldBlockSunriseLobbyMusicStart()
    {
        return _state.CurrentState is not LobbyState;
    }
}
