using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Sunrise.TapePlayer;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.TapePlayer;

public sealed partial class TapePlayerSystem : SharedTapePlayerSystem
{
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private AppearanceSystem _appearanceSystem = default!;

    private readonly List<ICommonSession> _ignoredRecipients = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TapePlayerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TapePlayerComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<TapePlayerComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<TapePlayerComponent, EntRemovedFromContainerMessage>(OnItemRemoved);

        SubscribeLocalEvent<TapePlayerComponent, TapePlayerPlayingMessage>(OnTapePlayerPlay);
        SubscribeLocalEvent<TapePlayerComponent, TapePlayerPauseMessage>(OnTapePlayerPause);
        SubscribeLocalEvent<TapePlayerComponent, TapePlayerStopMessage>(OnTapePlayerStop);
        SubscribeLocalEvent<TapePlayerComponent, TapePlayerSetTimeMessage>(OnTapePlayerSetTime);
        SubscribeLocalEvent<TapePlayerComponent, TapePlayerSetVolumeMessage>(OnTapePlayerSetVolume);

        SubscribeLocalEvent<TapePlayerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeNetworkEvent<ClientOptionTapePlayerEvent>(OnClientOptionTapePlayer);
    }

    private async void OnClientOptionTapePlayer(ClientOptionTapePlayerEvent ev, EntitySessionEventArgs args)
    {
        if (ev.Enabled)
            _ignoredRecipients.Remove(args.SenderSession);
        else
            _ignoredRecipients.Add(args.SenderSession);
    }

    private void OnItemInserted(EntityUid uid, TapePlayerComponent component, EntInsertedIntoContainerMessage args)
    {
        component.InsertedTape = args.Entity;
        Dirty(uid, component);
    }

    private void OnItemRemoved(EntityUid uid, TapePlayerComponent component, EntRemovedFromContainerMessage args)
    {
        _audioSystem.Stop(component.AudioStream);
        component.AudioStream = null;
        component.InsertedTape = null;
        Dirty(uid, component);
    }

    private void OnMapInit(EntityUid uid, TapePlayerComponent component, MapInitEvent args)
    {
        _itemSlotsSystem.AddItemSlot(uid, TapePlayerComponent.TapeSlotId, component.TapeSlot);
        if (HasComp<ApcPowerReceiverComponent>(uid))
        {
            TryUpdateVisualState(uid, component);
        }
    }

    private void OnTapePlayerPlay(EntityUid uid, TapePlayerComponent component, ref TapePlayerPlayingMessage args)
    {
        _audioSystem.PlayPvs(component.ButtonSound, uid);
        if (Exists(component.AudioStream))
        {
            Audio.SetState(component.AudioStream, AudioState.Playing);
        }
        else
        {
            component.AudioStream = Audio.Stop(component.AudioStream);

            if (!TryComp<MusicTapeComponent>(component.TapeSlot.Item, out var musicTapeComponent))
            {
                return;
            }

            var volume = SharedAudioSystem.GainToVolume(component.Volume) + component.IncreaceVolume;

            var audioParams = AudioParams.Default
                .WithVolume(volume)
                .WithMaxDistance(component.MaxDistance)
                .WithRolloffFactor(component.RolloffFactor)
                .WithLoop(component.Loop);
            var filter = Filter.Pvs(uid).RemovePlayers(_ignoredRecipients);
            var audio = Audio.PlayEntity(
                musicTapeComponent.Sound,
                filter,
                uid,
                false,
                audioParams);
            if (audio != null)
                component.AudioStream = audio.Value.Entity;
            Dirty(uid, component);
        }
    }

    private void OnTapePlayerPause(Entity<TapePlayerComponent> ent, ref TapePlayerPauseMessage args)
    {
        _audioSystem.PlayPvs(ent.Comp.ButtonSound, ent.Owner);
        Audio.SetState(ent.Comp.AudioStream, AudioState.Paused);
    }

    private void OnTapePlayerSetTime(EntityUid uid, TapePlayerComponent component, TapePlayerSetTimeMessage args)
    {
        if (TryComp(args.Actor, out ActorComponent? actorComp))
        {
            var offset = actorComp.PlayerSession.Channel.Ping * 1.5f / 1000f;
            Audio.SetPlaybackPosition(component.AudioStream, args.SongTime + offset);
        }
    }

    private void OnTapePlayerSetVolume(EntityUid uid, TapePlayerComponent component, TapePlayerSetVolumeMessage args)
    {
        component.Volume = args.Volume;
        var volume = SharedAudioSystem.GainToVolume(component.Volume) + component.IncreaceVolume;
        Audio.SetVolume(component.AudioStream, volume);
        Dirty(uid, component);
    }

    private void OnPowerChanged(Entity<TapePlayerComponent> entity, ref PowerChangedEvent args)
    {
        if (!entity.Comp.NeedPower)
            return;

        TryUpdateVisualState(entity);

        if (!this.IsPowered(entity.Owner, EntityManager))
        {
            Stop(entity);
        }
    }

    private void OnTapePlayerStop(Entity<TapePlayerComponent> ent, ref TapePlayerStopMessage args)
    {
        _audioSystem.PlayPvs(ent.Comp.ButtonSound, ent.Owner);
        Stop(ent);
    }

    private void Stop(Entity<TapePlayerComponent> entity)
    {
        Audio.SetState(entity.Comp.AudioStream, AudioState.Stopped);
        Dirty(entity);
    }

    private void OnComponentShutdown(EntityUid uid, TapePlayerComponent component, ComponentShutdown args)
    {
        component.AudioStream = Audio.Stop(component.AudioStream);
    }

    private void TryUpdateVisualState(EntityUid uid, TapePlayerComponent? jukeboxComponent = null)
    {
        if (!Resolve(uid, ref jukeboxComponent))
            return;

        var finalState = TapePlayerVisualState.On;

        if (!this.IsPowered(uid, EntityManager))
        {
            finalState = TapePlayerVisualState.Off;
        }

        _appearanceSystem.SetData(uid, TapePlayerVisuals.VisualState, finalState);
    }
}
