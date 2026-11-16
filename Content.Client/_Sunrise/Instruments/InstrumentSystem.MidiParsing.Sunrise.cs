using System.Diagnostics.CodeAnalysis;
using Content.Client.Instruments.MidiParser;
using Content.Shared.Instruments;

// ReSharper disable once CheckNamespace
namespace Content.Client.Instruments;

public sealed partial class InstrumentSystem
{
    // безопасно разбирает midi для получения названий дорожек
    private static bool TryGetMidiTracks(
        byte[] data,
        [NotNullWhen(true)] out MidiTrack[]? tracks,
        [NotNullWhen(false)] out string? error)
    {
        try
        {
            return MidiParser.MidiParser.TryGetMidiTracks(data, out tracks, out error);
        }
        catch (Exception exception)
        {
            tracks = null;
            error = exception.Message;
            return false;
        }
    }
}
