using Content.Server.Popups;
using Content.Server.Speech.EntitySystems;
using Content.Shared.Abilities.Mime;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Puppet;
using Content.Shared.Speech;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Speech.Muting;

/// <summary>
/// Bridges the new status-effect mute used by Sunrise anti-spam into the existing speech pipeline
/// without modifying the vanilla muting system.
/// </summary>
public sealed partial class SunriseMutedStatusEffectSystem : EntitySystem
{
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StatusEffectContainerComponent, SpeakAttemptEvent>(OnSpeakAttempt);
        SubscribeLocalEvent<StatusEffectContainerComponent, EmoteEvent>(OnEmote, before: [typeof(VocalSystem), typeof(MumbleAccentSystem)]);
        SubscribeLocalEvent<StatusEffectContainerComponent, EmoteActionEvent>(OnEmoteAction, before: [typeof(VocalSystem)]);
    }

    private void OnSpeakAttempt(EntityUid uid, StatusEffectContainerComponent component, SpeakAttemptEvent args)
    {
        if (args.Cancelled || HasComp<MutedComponent>(uid) || !HasMutedStatusEffect(component))
            return;

        PopupMuted(uid);
        args.Cancel();
    }

    private void OnEmote(EntityUid uid, StatusEffectContainerComponent component, ref EmoteEvent args)
    {
        if (args.Handled || HasComp<MutedComponent>(uid) || !HasMutedStatusEffect(component))
            return;

        if (args.Emote.Category.HasFlag(EmoteCategory.Vocal))
            args.Handled = true;
    }

    private void OnEmoteAction(EntityUid uid, StatusEffectContainerComponent component, EmoteActionEvent args)
    {
        if (args.Handled || HasComp<MutedComponent>(uid) || !HasMutedStatusEffect(component))
            return;

        if (!_prototypeManager.Resolve(args.Emote, out var emote) ||
            !emote.Category.HasFlag(EmoteCategory.Vocal))
        {
            return;
        }

        PopupMuted(uid);
        args.Handled = true;
    }

    private bool HasMutedStatusEffect(StatusEffectContainerComponent component)
    {
        foreach (var effect in component.ActiveStatusEffects?.ContainedEntities ?? [])
        {
            if (HasComp<MutedComponent>(effect))
                return true;
        }

        return false;
    }

    private void PopupMuted(EntityUid uid)
    {
        if (HasComp<MimePowersComponent>(uid))
            _popupSystem.PopupEntity(Loc.GetString("mime-cant-speak"), uid, uid);
        else if (HasComp<VentriloquistPuppetComponent>(uid))
            _popupSystem.PopupEntity(Loc.GetString("ventriloquist-puppet-cant-speak"), uid, uid);
        else
            _popupSystem.PopupEntity(Loc.GetString("speech-muted"), uid, uid);
    }
}
