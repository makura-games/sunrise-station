using Content.Shared._Sunrise.TapeRecorder;
using Content.Shared.Speech.Components;

namespace Content.Server._Sunrise.TapeRecorder;

public sealed partial class TapeRecorderSystem
{
    public bool TrySetMode(Entity<TapeRecorderComponent> ent, TapeRecorderMode mode, EntityUid user)
    {
        if (!IsValidMode(mode))
            return false;

        if (mode == TapeRecorderMode.Stopped)
        {
            Stop(ent);
            _audio.PlayPvs(ent.Comp.ButtonSound, ent.Owner);
            return true;
        }

        if (!CanSetMode(ent, mode, user))
            return false;

        UpdateRecorder(ent);

        ent.Comp.Mode = mode;
        ent.Comp.LastUpdateTime = _timing.CurTime;
        ent.Comp.NextPlaybackLineTime = TimeSpan.Zero;

        if (mode == TapeRecorderMode.Recording)
        {
            var listener = EnsureComp<ActiveListenerComponent>(ent);
            listener.Range = ent.Comp.RecordingRange;
        }
        else
        {
            RemComp<ActiveListenerComponent>(ent);
        }

        EnsureComp<ActiveTapeRecorderComponent>(ent);
        _audio.PlayPvs(ent.Comp.ButtonSound, ent.Owner);
        Dirty(ent);
        return true;
    }

    public bool CanSetMode(Entity<TapeRecorderComponent> ent, TapeRecorderMode mode, EntityUid user, bool quiet = false)
    {
        if (!TryGetCassette(ent, out var cassette))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("tape-recorder-popup-no-cassette"), ent, user);
            return false;
        }

        if (mode == TapeRecorderMode.Recording && cassette.Comp.Position >= cassette.Comp.Capacity)
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("tape-recorder-popup-tape-full"), ent, user);
            return false;
        }

        if (mode == TapeRecorderMode.Recording && IsTapePositionUsed(cassette, cassette.Comp.Position))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("tape-recorder-popup-tape-used"), ent, user);
            return false;
        }

        if (mode == TapeRecorderMode.Playing && !HasUsedTape(cassette))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("tape-recorder-popup-tape-empty"), ent, user);
            return false;
        }

        return true;
    }

    private void Stop(Entity<TapeRecorderComponent> ent)
    {
        ent.Comp.Mode = TapeRecorderMode.Stopped;
        ent.Comp.LastUpdateTime = _timing.CurTime;
        RemComp<ActiveListenerComponent>(ent);
        RemComp<ActiveTapeRecorderComponent>(ent);
        Dirty(ent);
    }

    private static bool IsValidMode(TapeRecorderMode mode) =>
        Enum.IsDefined(typeof(TapeRecorderMode), mode);
}
