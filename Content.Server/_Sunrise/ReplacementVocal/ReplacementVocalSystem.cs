using Content.Shared.Humanoid;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.ReplacementVocal;

public sealed partial class ReplacementVocalSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ReplacementVocalComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ReplacementVocalComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnComponentInit(EntityUid uid, ReplacementVocalComponent component, ComponentInit args)
    {
        if (!TryComp<VocalComponent>(uid, out var vocalComponent))
            return;

        if (component.Vocal.Keys == vocalComponent.Sounds?.Keys)
            return;

        if (!TryComp<SpeechComponent>(uid, out var speechComponent))
            return;

        component.PreviousVocal = vocalComponent.Sounds;

        vocalComponent.Sounds = component.Vocal;

        LoadEmotes(uid, vocalComponent);

        if (!_proto.TryIndex(vocalComponent.EmoteSounds, out var soundIndex))
            return;

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
        if (!TryComp<SpeechComponent>(uid, out var speech))
            return;

        if (!TryComp<VocalComponent>(uid, out var vocal))
            return;

        if (component.PreviousVocal != null)
            vocal.Sounds = component.PreviousVocal;

        LoadEmotes(uid, vocal);

        foreach (var emote in component.AddedEmotes)
        {
            speech.AllowedEmotes.Remove(emote);
        }

        component.AddedEmotes.Clear();
    }

    private void LoadEmotes(EntityUid uid, VocalComponent vocalComponent)
    {
        var sex = CompOrNull<HumanoidProfileComponent>(uid)?.Sex ?? Sex.Unsexed;

        if (vocalComponent.Sounds == null)
            return;

        if (!vocalComponent.Sounds.TryGetValue(sex, out var protoId))
            return;

        if (!_proto.HasIndex(protoId))
            return;

        vocalComponent.EmoteSounds = protoId;
    }
}
