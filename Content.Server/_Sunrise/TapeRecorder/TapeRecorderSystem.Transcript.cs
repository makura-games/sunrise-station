using System.Text;
using Content.Shared._Sunrise.TapeRecorder;
using Content.Shared.Paper;

namespace Content.Server._Sunrise.TapeRecorder;

public sealed partial class TapeRecorderSystem
{
    private bool TryPrintTranscript(Entity<TapeRecorderComponent> ent, EntityUid user)
    {
        if (_timing.CurTime < ent.Comp.NextPrintTime)
            return false;

        if (!TryGetCassette(ent, out var cassette))
        {
            _popup.PopupEntity(Loc.GetString("tape-recorder-popup-no-cassette"), ent, user);
            return false;
        }

        if (cassette.Comp.Records.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("tape-recorder-popup-tape-empty"), ent, user);
            return false;
        }

        var paper = Spawn(ent.Comp.PaperPrototype, Transform(user).Coordinates);
        if (!TryComp<PaperComponent>(paper, out var paperComp))
            return false;

        _paper.SetContent((paper, paperComp), BuildTranscript(cassette));
        ent.Comp.NextPrintTime = _timing.CurTime + ent.Comp.PrintCooldown;
        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
        Dirty(ent);
        return true;
    }

    private string BuildTranscript(Entity<TapeCassetteComponent> cassette)
    {
        var transcript = new StringBuilder();

        foreach (var record in cassette.Comp.Records)
        {
            var serverTime = FormatTime(record.RecordedAt);
            var pos = FormatPosition(record.Time);

            transcript.AppendLine(Loc.GetString(
                "tape-recorder-transcript-line",
                ("time", serverTime),
                ("position", pos),
                ("speaker", record.Speaker),
                ("message", record.Message)));
        }

        return transcript.ToString().TrimEnd();
    }

    private static string FormatTime(TimeSpan time) =>
        time.ToString(@"hh\:mm\:ss");

    private static string FormatPosition(TimeSpan time) =>
        time.ToString(@"mm\:ss");
}
