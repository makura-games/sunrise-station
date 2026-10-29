using Content.Server._Sunrise.TTS;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Implants.Components;
using Content.Shared.Clothing;
using Content.Shared.VoiceMask;
using Content.Shared.Silicons.StationAi;

namespace Content.Server.VoiceMask;

public partial class VoiceMaskSystem
{
    private void InitializeSunriseVoiceMask()
    {
        SubscribeLocalEvent<TTSComponent, TransformSpeakerVoiceEvent>(OnSpeakerVoiceTransform);
        SubscribeLocalEvent<VoiceMaskComponent, VoiceMaskChangeVoiceMessage>(OnChangeVoice);
        SubscribeLocalEvent<VoiceMaskComponent, ClothingGotUnequippedEvent>(OnSunriseVoiceMaskUnequipped);
    }

    private void OnSunriseVoiceMaskEquipped(EntityUid wearer, VoiceMaskComponent component)
    {
        EnsureComp<VoiceMaskerComponent>(wearer, out var maskerComponent);
        maskerComponent.VoiceId = component.VoiceId;
    }

    private void OnSunriseVoiceMaskUnequipped(Entity<VoiceMaskComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        RemCompDeferred<VoiceMaskerComponent>(args.Wearer);
    }

    private VoiceMaskBuiState CreateSunriseVoiceMaskBuiState(Entity<VoiceMaskComponent> entity)
    {
        return new VoiceMaskBuiState(
            GetCurrentVoiceName(entity),
            entity.Comp.VoiceId,
            entity.Comp.VoiceMaskSpeechVerb,
            entity.Comp.Active,
            entity.Comp.AccentHide,
            entity.Comp.TitleText);
    }

    private void OnSpeakerVoiceTransform(EntityUid uid, TTSComponent component, TransformSpeakerVoiceEvent args)
    {
        if (TryComp<VoiceMaskerComponent>(uid, out var maskerComponent))
            args.VoiceId = maskerComponent.VoiceId;

        // Голосовой имплант находится в отдельном контейнере и не получает TTS-событие через общий implant relay.
        if (!_container.TryGetContainer(uid, ImplanterComponent.ImplantSlotId, out var implantContainer))
            return;

        foreach (var implant in implantContainer.ContainedEntities)
        {
            if (TryComp<VoiceMaskComponent>(implant, out var voiceMask) && voiceMask.Active)
            {
                args.VoiceId = voiceMask.VoiceId;
                break;
            }
        }
    }

    private void OnChangeVoice(EntityUid uid, VoiceMaskComponent component, VoiceMaskChangeVoiceMessage message)
    {
        if (!_proto.TryIndex<TTSVoicePrototype>(message.Voice, out var voiceProto))
            return;

        if (HasComp<StationAiHeldComponent>(message.Actor))
        {
            _popupSystem.PopupEntity(Loc.GetString("voice-mask-ai-cannot-use-this-voice"), uid);
            return;
        }

        component.VoiceId = message.Voice;

        if (TryComp<VoiceMaskerComponent>(message.Actor, out var maskerComponent))
            maskerComponent.VoiceId = message.Voice;

        _popupSystem.PopupEntity(Loc.GetString("voice-mask-voice-popup-success"), uid);

        UpdateUI((uid, component));
    }
}
