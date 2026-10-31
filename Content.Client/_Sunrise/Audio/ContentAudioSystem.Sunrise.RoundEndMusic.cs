using Content.Client.Lobby;
using Content.Shared._Sunrise.Audio.Events;
using Content.Shared.CCVar;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Audio;

public sealed partial class ContentAudioSystem
{
    private readonly List<RoundEndMusicTrack> _roundEndMusicTracks = [];
    private EntityUid? _roundEndAudioStream;
    private int? _lastRoundEndMusicTrackIndex;

    private void OnRoundEndMusic(RoundEndMusicEvent ev)
    {
        EndLobbyMusic();
        _lobbyPlaylist = null;
        StopRoundEndMusic();
        _roundEndMusicTracks.Clear();
        _roundEndMusicTracks.AddRange(ev.Tracks);
        _lastRoundEndMusicTrackIndex = null;

        if (!_configManager.GetCVar(SunriseCCVars.RoundEndMusicEnabled))
            return;

        StartNextRoundEndMusic();
    }

    private void StopRoundEndMusic()
    {
        _roundEndAudioStream = _audio.Stop(_roundEndAudioStream);
    }

    private void HandleSunriseRoundEndMusicCleanup()
    {
        _roundEndMusicTracks.Clear();
        _lastRoundEndMusicTrackIndex = null;
        _roundEndAudioStream = null;
    }

    private void UpdateSunriseRoundEndMusic()
    {
        if (_roundEndMusicTracks.Count == 0)
            return;

        if (_state.CurrentState is LobbyState)
            return;

        if (!_configManager.GetCVar(SunriseCCVars.RoundEndMusicEnabled))
        {
            StopRoundEndMusic();
            return;
        }

        if (TryComp<AudioComponent>(_roundEndAudioStream, out var audioComp) && audioComp.Playing)
            return;

        StartNextRoundEndMusic();
    }

    private void StartNextRoundEndMusic()
    {
        var attemptedTracks = new HashSet<int>();

        while (TryPickRoundEndMusicTrack(attemptedTracks, out var trackIndex))
        {
            attemptedTracks.Add(trackIndex);

            var resolved = _audio.ResolveSound(_roundEndMusicTracks[trackIndex].Music);
            if (ResolvedSoundSpecifier.IsNullOrEmpty(resolved))
                continue;

            var stream = _audio.PlayGlobal(
                resolved,
                Filter.Local(),
                false,
                _roundEndMusicTracks[trackIndex]
                    .Music.Params
                    .AddVolume(SharedAudioSystem.GainToVolume(_configManager.GetCVar(CCVars.LobbyMusicVolume))));

            if (stream == null)
                continue;

            _lastRoundEndMusicTrackIndex = trackIndex;
            _roundEndAudioStream = stream.Value.Entity;
            return;
        }
    }

    private bool TryPickRoundEndMusicTrack(HashSet<int> attemptedTracks, out int trackIndex)
    {
        trackIndex = -1;

        if (_roundEndMusicTracks.Count == 0)
            return false;

        var excludeLastTrack = false;
        for (var i = 0; i < _roundEndMusicTracks.Count; i++)
        {
            if (attemptedTracks.Contains(i) || _lastRoundEndMusicTrackIndex == i)
                continue;

            if (_roundEndMusicTracks[i].Weight > 0f)
            {
                excludeLastTrack = true;
                break;
            }
        }

        var totalWeight = 0f;
        for (var i = 0; i < _roundEndMusicTracks.Count; i++)
        {
            if (attemptedTracks.Contains(i))
                continue;

            if (excludeLastTrack && _lastRoundEndMusicTrackIndex == i)
                continue;

            var weight = _roundEndMusicTracks[i].Weight;
            if (weight <= 0f)
                continue;

            totalWeight += weight;
        }

        if (totalWeight <= 0f)
        {
            for (var i = 0; i < _roundEndMusicTracks.Count; i++)
            {
                if (attemptedTracks.Contains(i))
                    continue;

                if (excludeLastTrack && _lastRoundEndMusicTrackIndex == i)
                    continue;

                trackIndex = i;
                return true;
            }

            return false;
        }

        var roll = _random.NextFloat() * totalWeight;
        var cumulative = 0f;

        for (var i = 0; i < _roundEndMusicTracks.Count; i++)
        {
            if (attemptedTracks.Contains(i))
                continue;

            if (excludeLastTrack && _lastRoundEndMusicTrackIndex == i)
                continue;

            var weight = _roundEndMusicTracks[i].Weight;
            if (weight <= 0f)
                continue;

            cumulative += weight;
            if (roll > cumulative)
                continue;

            trackIndex = i;
            return true;
        }

        for (var i = 0; i < _roundEndMusicTracks.Count; i++)
        {
            if (attemptedTracks.Contains(i))
                continue;

            if (excludeLastTrack && _lastRoundEndMusicTrackIndex == i)
                continue;

            if (_roundEndMusicTracks[i].Weight <= 0f)
                continue;

            trackIndex = i;
            return true;
        }

        return false;
    }
}
