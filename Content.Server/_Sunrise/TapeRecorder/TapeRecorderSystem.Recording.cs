using Content.Shared._Sunrise.TapeRecorder;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Speech;
using Content.Server._Sunrise.TTS;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.TapeRecorder;

public sealed partial class TapeRecorderSystem
{
    private void OnListen(Entity<TapeRecorderComponent> ent, ref ListenEvent args)
    {
        if (ent.Comp.Mode != TapeRecorderMode.Recording)
            return;

        if (!TryGetCassette(ent, out var cassette))
            return;

        UpdateRecorder(ent);

        if (cassette.Comp.Position >= cassette.Comp.Capacity)
        {
            Stop(ent);
            return;
        }

        if (cassette.Comp.Records.Count >= cassette.Comp.MaxRecords)
            cassette.Comp.Records.RemoveAt(0);

        cassette.Comp.Records.Add(new TapeCassetteRecord(
            cassette.Comp.Position,
            GetSpeakerName(ent, args.Source),
            args.Message,
            GetSpeakerVoice(args.Source),
            _timing.CurTime));

        Dirty(cassette);
    }

    private string GetSpeakerName(Entity<TapeRecorderComponent> recorder, EntityUid source) =>
        Exists(source) ? Name(source) : Loc.GetString(recorder.Comp.UnknownSpeaker);

    private ProtoId<TTSVoicePrototype>? GetSpeakerVoice(EntityUid source)
    {
        if (!TryComp<TTSComponent>(source, out var ttsComponent))
            return null;

        var voiceId = ttsComponent.VoicePrototypeId;
        if (voiceId == null || string.IsNullOrWhiteSpace(voiceId.Value))
            return null;

        var voiceEv = new TransformSpeakerVoiceEvent(source, voiceId.Value);
        RaiseLocalEvent(source, voiceEv);
        return voiceEv.VoiceId;
    }
}
