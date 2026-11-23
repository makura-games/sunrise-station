using Content.Shared._Sunrise.TapeRecorder;
using Robust.Shared.Maths;

namespace Content.Server._Sunrise.TapeRecorder;

public sealed partial class TapeRecorderSystem
{
    private bool TryGetCassette(Entity<TapeRecorderComponent> recorder, out Entity<TapeCassetteComponent> cassette)
    {
        cassette = default;
        var cassetteUid = recorder.Comp.Cassette;
        if (cassetteUid == null)
            return false;

        if (!TryComp<TapeCassetteComponent>(cassetteUid, out var cassetteComp))
            return false;

        cassette = (cassetteUid.Value, cassetteComp);
        return true;
    }

    private static bool IsTapePositionUsed(Entity<TapeCassetteComponent> cassette, TimeSpan position)
    {
        foreach (var range in cassette.Comp.RecordedRanges)
        {
            if (position >= range.Start && position < range.End)
                return true;
        }

        return false;
    }

    private static bool HasUsedTape(Entity<TapeCassetteComponent> cassette) =>
        cassette.Comp.Records.Count > 0 || cassette.Comp.RecordedRanges.Count > 0;

    private static TimeSpan GetNextUsedPosition(Entity<TapeCassetteComponent> cassette, TimeSpan position)
    {
        var next = cassette.Comp.Capacity;
        foreach (var range in cassette.Comp.RecordedRanges)
        {
            if (range.Start > position && range.Start < next)
                next = range.Start;
        }

        return next;
    }

    private static void AddRecordedRange(Entity<TapeCassetteComponent> cassette, TimeSpan start, TimeSpan end)
    {
        if (end <= start)
            return;

        cassette.Comp.RecordedRanges.Add(new TapeCassetteRecordedRange(start, end));
        cassette.Comp.RecordedRanges.Sort(static (a, b) => a.Start.CompareTo(b.Start));

        for (var i = 0; i < cassette.Comp.RecordedRanges.Count - 1; i++)
        {
            var current = cassette.Comp.RecordedRanges[i];
            var next = cassette.Comp.RecordedRanges[i + 1];
            if (current.End < next.Start)
                continue;

            cassette.Comp.RecordedRanges[i] = new TapeCassetteRecordedRange(current.Start, MathHelper.Max(current.End, next.End));
            cassette.Comp.RecordedRanges.RemoveAt(i + 1);
            i--;
        }
    }
}
