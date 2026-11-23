using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chat;
using Content.Shared.Interaction;
using Content.Shared.Inventory.Events;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using Content.Shared._Sunrise.TTS;
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Radio.EntitySystems;

public sealed partial class HeadsetSystem : SharedHeadsetSystem
{
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HeadsetComponent, RadioReceiveEvent>(OnHeadsetReceive);
        SubscribeLocalEvent<HeadsetComponent, EncryptionChannelsChangedEvent>(OnKeysChanged);
        // Sunrise-Start
        SubscribeLocalEvent<HeadsetComponent, ActivateInWorldEvent>(OnActivate);
        // Sunrise-End
        SubscribeLocalEvent<WearingHeadsetComponent, EntitySpokeEvent>(OnSpeak);
    }

    private void OnKeysChanged(Entity<HeadsetComponent> ent, ref EncryptionChannelsChangedEvent args)
    {
        UpdateRadioChannels(ent, ent.Comp, args.Component);
    }

    private void UpdateRadioChannels(EntityUid uid, HeadsetComponent headset, EncryptionKeyHolderComponent? keyHolder = null)
    {
        // make sure to not add ActiveRadioComponent when headset is being deleted
        if (!headset.Enabled || MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        if (!Resolve(uid, ref keyHolder))
            return;

        if (keyHolder.Channels.Count == 0)
            RemComp<ActiveRadioComponent>(uid);
        else
            EnsureComp<ActiveRadioComponent>(uid).Channels = new(keyHolder.Channels);
    }

    private void OnSpeak(EntityUid uid, WearingHeadsetComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null
            && TryComp(component.Headset, out EncryptionKeyHolderComponent? keys)
            && keys.Channels.Contains(args.Channel.ID))
        {
            _radio.SendRadioMessage(uid, args.Message, args.Channel, component.Headset);
            args.Channel = null; // prevent duplicate messages from other listeners.
        }
    }

    protected override void OnGotEquipped(Entity<HeadsetComponent> ent, ref GotEquippedEvent args)
    {
        base.OnGotEquipped(ent, ref args);
        if (ent.Comp.IsEquipped && ent.Comp.Enabled)
        {
            EnsureComp<WearingHeadsetComponent>(args.EquipTarget).Headset = ent;
            UpdateRadioChannels(ent, ent.Comp);
            _actions.AddAction(args.EquipTarget, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction, ent); // Sunrise-Add
        }
    }

    protected override void OnGotUnequipped(Entity<HeadsetComponent> ent, ref GotUnequippedEvent args)
    {
        base.OnGotUnequipped(ent, ref args);
        RemComp<ActiveRadioComponent>(ent);
        RemComp<WearingHeadsetComponent>(args.EquipTarget);
        // Sunrise-Start
        if (TryComp<ActionComponent>(ent.Comp.ToggleActionEntity, out var action) && action.AttachedEntity == args.EquipTarget)
        {
            _actions.RemoveAction(args.EquipTarget, ent.Comp.ToggleActionEntity);
        }
        // Sunrise-End
    }

    public void SetEnabled(EntityUid uid, bool value, HeadsetComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Enabled == value)
            return;

        component.Enabled = value;
        Dirty(uid, component);

        if (!value)
        {
            RemCompDeferred<ActiveRadioComponent>(uid);

            if (component.IsEquipped)
            {
                // Sunrise-Start
                var parent = Transform(uid).ParentUid;
                RemCompDeferred<WearingHeadsetComponent>(parent);
                if (TryComp<ActionComponent>(component.ToggleActionEntity, out var action) && action.AttachedEntity == parent)
                {
                    _actions.RemoveAction(parent, component.ToggleActionEntity);
                }
                // Sunrise-End
            }
        }
        else if (component.IsEquipped)
        {
            // Sunrise-Start
            var parent = Transform(uid).ParentUid;
            EnsureComp<WearingHeadsetComponent>(parent).Headset = uid;
            UpdateRadioChannels(uid, component);
            _actions.AddAction(parent, ref component.ToggleActionEntity, component.ToggleAction, uid);
            // Sunrise-End
        }
    }

    private void OnHeadsetReceive(Entity<HeadsetComponent> ent, ref RadioReceiveEvent args)
    {
        // Sunrise-Start
        if (!ent.Comp.EnabledChannels.GetValueOrDefault(args.Channel.ID, true))
            return;
        // Sunrise-End

        // TODO: change this when a code refactor is done
        // this is currently done this way because receiving radio messages on an entity otherwise requires that entity
        // to have an ActiveRadioComponent

        var parent = Transform(ent).ParentUid;

        if (parent.IsValid())
        {
            var relayEvent = new HeadsetRadioReceiveRelayEvent(args);
            RaiseLocalEvent(parent, ref relayEvent);
        }

        if (TryComp(parent, out ActorComponent? actor))
        {
            _netMan.ServerSendMessage(args.ChatMsg, actor.PlayerSession.Channel);
            if (parent != args.MessageSource && HasComp<TTSComponent>(args.MessageSource))
            {
                args.Receivers.Add(parent);
            }
        }
    }
}
