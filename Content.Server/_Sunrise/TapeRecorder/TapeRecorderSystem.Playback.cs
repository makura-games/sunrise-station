using Content.Shared._Sunrise.TapeRecorder;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Chat;
using Content.Shared.Speech.Components;
using Robust.Shared.Maths;

namespace Content.Server._Sunrise.TapeRecorder;

public sealed partial class TapeRecorderSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveTapeRecorderComponent, TapeRecorderComponent>();
        while (query.MoveNext(out var uid, out _, out var recorder))
        {
            UpdateRecorder((uid, recorder));
        }
    }

    private void UpdateRecorder(Entity<TapeRecorderComponent> ent)
    {
        if (!TryGetCassette(ent, out var cassette))
        {
            Stop(ent);
            return;
        }

        var now = _timing.CurTime;
        var elapsed = TimeSpan.FromSeconds(Math.Max(0, (now - ent.Comp.LastUpdateTime).TotalSeconds));
        ent.Comp.LastUpdateTime = now;

        switch (ent.Comp.Mode)
        {
            case TapeRecorderMode.Recording:
                var oldPosition = cassette.Comp.Position;
                var requestedPosition = MathHelper.Min(cassette.Comp.Capacity, cassette.Comp.Position + elapsed);
                var nextUsedPosition = GetNextUsedPosition(cassette, oldPosition);
                cassette.Comp.Position = MathHelper.Min(requestedPosition, nextUsedPosition);
                AddRecordedRange(cassette, oldPosition, cassette.Comp.Position);

                if (cassette.Comp.Position >= cassette.Comp.Capacity)
                {
                    Stop(ent);
                }
                else if (cassette.Comp.Position < requestedPosition)
                {
                    Stop(ent);
                }
                break;

            case TapeRecorderMode.Playing:
                oldPosition = cassette.Comp.Position;
                cassette.Comp.Position = MathHelper.Min(cassette.Comp.Capacity, cassette.Comp.Position + elapsed);
                PlayDueRecords(ent, cassette, oldPosition, cassette.Comp.Position);
                if (cassette.Comp.Position >= cassette.Comp.Capacity)
                    Stop(ent);
                break;

            case TapeRecorderMode.Rewinding:
                cassette.Comp.Position = MathHelper.Max(TimeSpan.Zero, cassette.Comp.Position - elapsed * ent.Comp.RewindSpeed);
                if (cassette.Comp.Position <= TimeSpan.Zero)
                    Stop(ent);
                break;
        }

        Dirty(ent);
        Dirty(cassette);
    }

    private void PlayDueRecords(
        Entity<TapeRecorderComponent> recorder,
        Entity<TapeCassetteComponent> cassette,
        TimeSpan oldPosition,
        TimeSpan newPosition)
    {
        if (_timing.CurTime < recorder.Comp.NextPlaybackLineTime)
            return;

        foreach (var record in cassette.Comp.Records)
        {
            if (record.Time < oldPosition || record.Time > newPosition)
                continue;

            recorder.Comp.NextPlaybackLineTime = _timing.CurTime + recorder.Comp.PlaybackLineCooldown;
            PlayRecord(recorder, record);
            return;
        }
    }

    private void PlayRecord(Entity<TapeRecorderComponent> recorder, TapeCassetteRecord record)
    {
        var hadTts = TryComp<TTSComponent>(recorder, out var tts);
        tts ??= EnsureComp<TTSComponent>(recorder);
        var oldVoice = tts.VoicePrototypeId;
        tts.VoicePrototypeId = record.VoiceId ?? recorder.Comp.PlaybackVoice;

        _chat.TrySendInGameICMessage(
            recorder,
            record.Message,
            InGameICChatType.Speak,
            ChatTransmitRange.Normal,
            nameOverride: GetPlaybackSpeakerName(recorder, record),
            checkRadioPrefix: false,
            ignoreActionBlocker: true);

        if (hadTts)
            tts.VoicePrototypeId = oldVoice;
        else
            tts.VoicePrototypeId = recorder.Comp.PlaybackVoice;
    }

    private string GetPlaybackSpeakerName(Entity<TapeRecorderComponent> recorder, TapeCassetteRecord record) =>
        $"{Name(recorder)} ({record.Speaker})";
}
