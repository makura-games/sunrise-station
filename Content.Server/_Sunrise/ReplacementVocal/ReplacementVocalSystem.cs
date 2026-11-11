using Content.Shared.Humanoid;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.ReplacementVocal;

public sealed partial class ReplacementVocalSystem : EntitySystem
{
    [Dependency] private VocalSystem _vocal = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ReplacementVocalComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ReplacementVocalComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, ReplacementVocalComponent component, ComponentInit args)
    {
        if (!TryComp<VocalComponent>(uid, out var vocalComponent))
            return;

        var sex = CompOrNull<HumanoidProfileComponent>(uid)?.Sex ?? Sex.Unsexed;
        if (!component.Vocal.TryGetValue(sex, out var replacement) ||
            !ProtoMan.TryIndex(replacement, out var soundIndex))
            return;

        if (vocalComponent.EmoteSounds == replacement)
            return;

        if (!TryComp<SpeechComponent>(uid, out var speechComponent))
            return;

        component.PreviousVocal = vocalComponent.EmoteSounds;
        _vocal.SetEmoteSounds((uid, vocalComponent), replacement);
        component.WasReplaced = true;

        foreach (var emote in soundIndex.Sounds.Keys)
        {
            if (speechComponent.AllowedEmotes.Contains(emote))
                continue;

            speechComponent.AllowedEmotes.Add(emote);
            component.AddedEmotes.Add(emote);
        }
    }

    private void OnComponentShutdown(EntityUid uid, ReplacementVocalComponent component, ComponentShutdown args)
    {
        if (component.WasReplaced && TryComp<VocalComponent>(uid, out var vocal))
            _vocal.SetEmoteSounds((uid, vocal), component.PreviousVocal);

        if (!TryComp<SpeechComponent>(uid, out var speech))
            return;

        foreach (var emote in component.AddedEmotes)
        {
            speech.AllowedEmotes.Remove(emote);
        }

        component.AddedEmotes.Clear();
    }
}
