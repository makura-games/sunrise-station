using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared._Sunrise.TapeRecorder;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Paper;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.TapeRecorder;

public sealed partial class TapeRecorderSystem : SharedTapeRecorderSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TapeRecorderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TapeRecorderComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TapeRecorderComponent, EntInsertedIntoContainerMessage>(OnCassetteInserted);
        SubscribeLocalEvent<TapeRecorderComponent, EntRemovedFromContainerMessage>(OnCassetteRemoved);
        SubscribeLocalEvent<TapeRecorderComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<TapeCassetteComponent, MapInitEvent>(OnCassetteMapInit);

        Subs.BuiEvents<TapeRecorderComponent>(TapeRecorderUiKey.Key, subs =>
        {
            subs.Event<TapeRecorderSetModeMessage>(OnSetMode);
            subs.Event<TapeRecorderPrintMessage>(OnPrint);
        });
    }

    #region Events

    private void OnMapInit(Entity<TapeRecorderComponent> ent, ref MapInitEvent args)
    {
        _itemSlots.AddItemSlot(ent, ent.Comp.CassetteSlotId, ent.Comp.CassetteSlot);
        ent.Comp.Cassette = ent.Comp.CassetteSlot.Item;
        Dirty(ent);
    }

    private void OnShutdown(Entity<TapeRecorderComponent> ent, ref ComponentShutdown args)
    {
        RemComp<ActiveListenerComponent>(ent);
        RemComp<ActiveTapeRecorderComponent>(ent);
    }

    private void OnCassetteInserted(Entity<TapeRecorderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.CassetteSlotId)
            return;

        OnCassetteChanged(ent, args.Entity);
    }

    private void OnCassetteRemoved(Entity<TapeRecorderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.CassetteSlotId)
            return;

        OnCassetteChanged(ent, null);
    }

    private void OnCassetteChanged(Entity<TapeRecorderComponent> ent, EntityUid? cassette)
    {
        ent.Comp.Cassette = cassette;
        Dirty(ent);
        Stop(ent);
    }

    private void OnSetMode(Entity<TapeRecorderComponent> ent, ref TapeRecorderSetModeMessage args) =>
        TrySetMode(ent, args.Mode, args.Actor);

    private void OnPrint(Entity<TapeRecorderComponent> ent, ref TapeRecorderPrintMessage args) =>
        TryPrintTranscript(ent, args.Actor);

    private void OnCassetteMapInit(Entity<TapeCassetteComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.MaxRecords = GetMaxRecords(ent);
        Dirty(ent);
    }

    #endregion

    private static int GetMaxRecords(Entity<TapeCassetteComponent> cassette)
    {
        var maxRecords = (int) (cassette.Comp.Capacity.TotalSeconds * cassette.Comp.RecordsPerSecond);
        return maxRecords > 0 ? maxRecords : TapeCassetteComponent.FallbackMaxRecords;
    }
}
