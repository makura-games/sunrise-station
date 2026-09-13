using Content.Shared._Sunrise.Humanoid;
using Content.Shared._Sunrise.Humanoid.Events;
using Content.Shared._Sunrise.TTS;

namespace Content.Server._Sunrise.Humanoid;

public sealed class SunriseHumanoidProfileTtsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseHumanoidProfileComponent, SunriseHumanoidTtsProfileChangedEvent>(OnProfileChanged);
    }

    private void OnProfileChanged(Entity<SunriseHumanoidProfileComponent> ent, ref SunriseHumanoidTtsProfileChangedEvent args)
    {
        var tts = EnsureComp<TTSComponent>(ent.Owner);
        tts.VoicePrototypeId = args.Voice;
    }
}
