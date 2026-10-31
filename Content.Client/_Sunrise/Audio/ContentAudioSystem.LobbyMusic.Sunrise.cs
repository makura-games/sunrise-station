using Content.Client.Lobby;
using Content.Client._Sunrise.Lobby;
using Content.Shared._Sunrise.Audio.Events;

#pragma warning disable IDE0130 // Namespace does not match folder structure
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
        return _state.CurrentState is not SunriseLobbyState;
    }
}
