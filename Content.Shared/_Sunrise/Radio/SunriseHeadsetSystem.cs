using Content.Shared.Chat;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;

namespace Content.Shared._Sunrise.Radio;

public sealed class SunriseHeadsetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadsetComponent, HeadsetToggleChannelMessage>(OnToggleChannel);
        SubscribeLocalEvent<HeadsetComponent, HeadsetChangeVolumeMessage>(OnChangeVolume);
        SubscribeLocalEvent<EncryptionKeyHolderComponent, EncryptionChannelsChangedEvent>(OnEncryptionChannelsChanged);
    }

    /// <summary>
    ///     When channels are recalculated, disable Common channel by default
    ///     unless the user has explicitly toggled it.
    /// </summary>
    private void OnEncryptionChannelsChanged(Entity<EncryptionKeyHolderComponent> ent, ref EncryptionChannelsChangedEvent args)
    {
        if (TryComp<HeadsetComponent>(ent, out var headset) &&
            args.Component.Channels.Contains(SharedChatSystem.CommonChannel) &&
            headset.EnabledChannels.TryAdd(SharedChatSystem.CommonChannel.Id, true))
        {
            Dirty(ent, headset);
        }
    }

    private void OnToggleChannel(Entity<HeadsetComponent> ent, ref HeadsetToggleChannelMessage args)
    {
        ent.Comp.EnabledChannels[args.ChannelId] = args.Enabled;
        Dirty(ent, ent.Comp);
    }

    private void OnChangeVolume(Entity<HeadsetComponent> ent, ref HeadsetChangeVolumeMessage args)
    {
        ent.Comp.ChannelVolumes[args.ChannelId] = args.Volume;
        Dirty(ent, ent.Comp);
    }
}
