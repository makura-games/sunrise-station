using Content.Shared._Sunrise;
using Content.Shared._Sunrise.Humanoid.Events;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Humanoid;

public sealed partial class SunriseHumanoidProfileSystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, ComponentStartup>(OnHumanoidProfileStartup);
    }

    private void OnHumanoidProfileStartup(Entity<HumanoidProfileComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SunriseHumanoidProfileComponent>(ent.Owner, out var profile))
            return;

        EnsureValidBodyType((ent.Owner, profile));
    }

    public void ApplyProfileTo(Entity<SunriseHumanoidProfileComponent?> ent, HumanoidCharacterProfile profile)
    {
        var component = EnsureComp<SunriseHumanoidProfileComponent>(ent.Owner);
        component.TtsVoice = profile.TtsVoice;
        component.BodyType = profile.BodyType;
        component.Width = profile.Width;
        component.Height = profile.Height;
        Dirty(ent.Owner, component);

        var changedEv = new SunriseHumanoidProfileChangedEvent(profile.Species, profile.TtsVoice, profile.BodyType, profile.Width, profile.Height);
        RaiseLocalEvent(ent.Owner, ref changedEv);

        var ttsChangedEv = new SunriseHumanoidTtsProfileChangedEvent(profile.TtsVoice);
        RaiseLocalEvent(ent.Owner, ref ttsChangedEv);
    }

    public void SetTTSVoice(EntityUid uid, ProtoId<TTSVoicePrototype> voiceId, SunriseHumanoidProfileComponent? profile = null)
    {
        profile ??= EnsureComp<SunriseHumanoidProfileComponent>(uid);

        profile.TtsVoice = voiceId;
        Dirty(uid, profile);
        RaiseProfileChanged(uid, profile);
    }

    public void SetBodyType(EntityUid uid, ProtoId<BodyTypePrototype> bodyType, bool sync = true, SunriseHumanoidProfileComponent? profile = null)
    {
        profile ??= EnsureComp<SunriseHumanoidProfileComponent>(uid);

        var speciesId = GetProfileSpecies(uid);
        var sex = GetProfileSex(uid);
        if (ProtoMan.TryIndex(speciesId, out var species) &&
            SunriseHumanoidProfileDefaults.IsBodyTypeAllowed(species, bodyType, sex, ProtoMan))
        {
            profile.BodyType = bodyType;
        }
        else
        {
            profile.BodyType = SunriseHumanoidProfileDefaults.GetDefaultBodyType(species, sex, ProtoMan);
        }

        if (!sync)
            return;

        Dirty(uid, profile);
        RaiseProfileChanged(uid, profile);
    }

    public void CloneProfile(EntityUid source, EntityUid target, SunriseHumanoidProfileComponent? sourceProfile = null, SunriseHumanoidProfileComponent? targetProfile = null)
    {
        if (!Resolve(source, ref sourceProfile, false))
            return;

        var component = targetProfile ?? EnsureComp<SunriseHumanoidProfileComponent>(target);
        component.TtsVoice = sourceProfile.TtsVoice;
        component.BodyType = sourceProfile.BodyType;
        component.Width = sourceProfile.Width;
        component.Height = sourceProfile.Height;
        Dirty(target, component);

        RaiseProfileChanged(target, component);
    }

    private ProtoId<SpeciesPrototype> GetProfileSpecies(EntityUid uid)
    {
        if (TryComp<HumanoidProfileComponent>(uid, out var profile))
            return profile.Species;

        return SunriseHumanoidProfileDefaults.DefaultSpecies;
    }

    private Sex GetProfileSex(EntityUid uid)
    {
        if (TryComp<HumanoidProfileComponent>(uid, out var profile))
            return profile.Sex;

        return Sex.Male;
    }

    private void EnsureValidBodyType(Entity<SunriseHumanoidProfileComponent> ent)
    {
        var speciesId = GetProfileSpecies(ent.Owner);
        var sex = GetProfileSex(ent.Owner);
        ProtoMan.TryIndex(speciesId, out SpeciesPrototype? species);

        if (species is not null &&
            SunriseHumanoidProfileDefaults.IsBodyTypeAllowed(species, ent.Comp.BodyType, sex, ProtoMan))
        {
            return;
        }

        var bodyType = SunriseHumanoidProfileDefaults.GetDefaultBodyType(species, sex, ProtoMan);
        if (ent.Comp.BodyType == bodyType)
            return;

        ent.Comp.BodyType = bodyType;
        Dirty(ent);
        RaiseProfileChanged(ent.Owner, ent.Comp);
    }

    private void RaiseProfileChanged(EntityUid uid, SunriseHumanoidProfileComponent profile)
    {
        var changedEv = new SunriseHumanoidProfileChangedEvent(
            GetProfileSpecies(uid),
            profile.TtsVoice,
            profile.BodyType,
            profile.Width,
            profile.Height);
        RaiseLocalEvent(uid, ref changedEv);

        var ttsChangedEv = new SunriseHumanoidTtsProfileChangedEvent(profile.TtsVoice);
        RaiseLocalEvent(uid, ref ttsChangedEv);
    }
}
