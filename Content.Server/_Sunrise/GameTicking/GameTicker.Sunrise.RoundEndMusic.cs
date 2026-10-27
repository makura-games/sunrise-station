using System.Diagnostics.CodeAnalysis;
using Content.Server._Sunrise.GameTicking.Events;
using Content.Shared._Sunrise.Audio.Events;
using Content.Shared.CCVar;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared._Sunrise.GameTicking.Prototypes;
using Robust.Shared.Audio;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [ViewVariables]
    private string? _roundEndMusicPool;

    private void InitializeSunriseCVars()
    {
        Subs.CVar(_cfg, SunriseCCVars.RoundEndMusicPool, value => _roundEndMusicPool = value, true);
    }

    private void RaiseRoundEndMusicEvent(TimeSpan roundDuration)
    {
        if (!TryResolveRoundEndMusic(roundDuration, out var tracks))
            return;

        RaiseNetworkEvent(new RoundEndMusicEvent(tracks));
    }

    private void RaiseRoundLobbyReadyEvent()
    {
        var ev = new RoundLobbyReadyEvent(RoundId);
        RaiseLocalEvent(ref ev);
    }

    private bool TryResolveRoundEndMusic(TimeSpan roundDuration, [NotNullWhen(true)] out List<RoundEndMusicTrack>? tracks)
    {
        tracks = null;

        var selection = new RoundEndMusicSelectionEvent(RoundId, roundDuration, CurrentPreset?.ID);
        RaiseLocalEvent(ref selection);

        if (selection.Handheld && !selection.Cancelled && selection.Sound != null)
        {
            tracks = [new RoundEndMusicTrack(selection.Sound)];
            return true;
        }

        if (TryResolveRoundEndMusicPool(_roundEndMusicPool, out tracks))
            return true;

        if (TryResolveRoundEndCollection(_cfg.GetCVar(CCVars.LobbyMusicCollection), out tracks))
            return true;

        return false;
    }

    private bool TryResolveRoundEndCollection(string? collectionId, [NotNullWhen(true)] out List<RoundEndMusicTrack>? tracks)
    {
        tracks = null;

        if (string.IsNullOrWhiteSpace(collectionId))
            return false;

        if (!_prototypeManager.TryIndex<SoundCollectionPrototype>(collectionId, out _))
        {
            Log.Warning($"Invalid round-end music sound collection specified: {collectionId}");
            return false;
        }

        tracks = new() {new RoundEndMusicTrack(new SoundCollectionSpecifier(collectionId))};
        return true;
    }

    private bool TryResolveRoundEndMusicPool(string? prototypeId, [NotNullWhen(true)] out List<RoundEndMusicTrack>? tracks)
    {
        tracks = null;

        if (string.IsNullOrWhiteSpace(prototypeId))
            return false;

        if (!_prototypeManager.TryIndex<RoundEndMusicPoolPrototype>(prototypeId, out var pool))
        {
            Log.Warning($"Invalid round-end music prototype specified: {prototypeId}");
            return false;
        }

        tracks = [];
        foreach (var track in pool.Tracks)
        {
            if (track.Weight <= 0f)
                continue;

            tracks.Add(new RoundEndMusicTrack(track.Sound, track.Weight));
        }

        if (tracks.Count == 0)
            return false;

        return true;
    }
}
