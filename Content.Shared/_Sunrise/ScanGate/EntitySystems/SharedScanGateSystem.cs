using Content.Shared._Sunrise.ScanGate.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Content.Shared.Inventory;
using Content.Shared.Hands;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.PowerCell;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Storage;

namespace Content.Shared._Sunrise.ScanGate.EntitySystems;

public sealed partial class SharedScanGateSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;

    private static readonly TimeSpan StateResetDelay = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        SubscribeLocalEvent<ScanGateComponent, StartCollideEvent>(OnCollide);

        // Detection events

        SubscribeLocalEvent<ScanDetectableComponent, TryDetectItem>(OnDetect);
        SubscribeLocalEvent<ScanDetectableComponent, InventoryRelayedEvent<TryDetectItem>>(OnInventoryRelay);
        Subs.SubscribeWithRelay<ScanDetectableComponent, HeldRelayedEvent<TryDetectItem>>(OnHandRelay, inventory: false);

        // Detect in storages

        SubscribeLocalEvent<StorageComponent, TryDetectItem>(OnDetectStorage);
        SubscribeLocalEvent<StorageComponent, InventoryRelayedEvent<TryDetectItem>>(OnInventoryRelayStorage); // Detect items in storage
        Subs.SubscribeWithRelay<StorageComponent, HeldRelayedEvent<TryDetectItem>>(OnHandRelayStorage, inventory: false); // Detect items in storage

        // Bypass events

        SubscribeLocalEvent<ScanByPassComponent, TryDetectItem>(OnBypass);
        SubscribeLocalEvent<ScanByPassComponent, InventoryRelayedEvent<TryDetectItem>>(OnInventoryRelayBypass);
        Subs.SubscribeWithRelay<ScanByPassComponent, HeldRelayedEvent<TryDetectItem>>(OnHandRelayBypass, inventory: false);

        base.Initialize();
    }

    #region Logic
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ScanGateComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.StateResetTime == TimeSpan.Zero)
                continue;

            if (component.StateResetTime > _timing.CurTime)
                continue;

            component.StateResetTime = TimeSpan.Zero;
            Dirty(uid, component);

            _appearance.SetData(uid, ScanGateVisuals.State, component.IdleState);
        }
    }

    private void OnCollide(Entity<ScanGateComponent> ent, ref StartCollideEvent args)
    {
        if (ent.Comp.NextScanTime > _timing.CurTime)
            return;

        if (!_powerReceiver.IsPowered(ent.Owner))
            return;

        ent.Comp.NextScanTime = _timing.CurTime + ent.Comp.ScanDelay;
        Dirty(ent);

        var ev = new TryDetectItem(ent.Owner);
        RaiseLocalEvent(args.OtherEntity, ref ev);

        if (ev.ByPass)
        {
            NoItemDetected(ent);
            return;
        }

        if (!ev.EntityDetected)
        {
            NoItemDetected(ent);
            return;
        }

        if (TryComp<AccessReaderComponent>(ent.Owner, out var accessReader)
            && _accessReader.IsAllowed(args.OtherEntity, ent.Owner, accessReader))
        {
            NoItemDetected(ent);
            return;
        }

        ItemDetected(ent);
    }

    #endregion

    #region Detection

    /// <summary>
    /// An entity with <see cref="ScanDetectableComponent"/> has been detected by a scan gate.
    /// </summary>
    private void OnDetect(Entity<ScanDetectableComponent> ent, ref TryDetectItem args)
    {
        args.EntityDetected = true;
    }

    /// <summary>
    /// An entity with <see cref="ScanDetectableComponent"/> has been detected by a scan gate.
    /// </summary>
    private void OnInventoryRelay(Entity<ScanDetectableComponent> ent, ref InventoryRelayedEvent<TryDetectItem> args)
    {
        args.Args.EntityDetected = true;
    }

    /// <summary>
    /// An entity with <see cref="ScanDetectableComponent"/> has been detected by a scan gate.
    /// </summary>
    private void OnHandRelay(Entity<ScanDetectableComponent> ent, ref HeldRelayedEvent<TryDetectItem> args)
    {
        args.Args.EntityDetected = true;
    }

    #endregion

    #region Storage Detection

    private void OnDetectStorage(Entity<StorageComponent> ent, ref TryDetectItem args)
    {
        if (args.ByPass) // No need to check if already bypassed
            return;

        foreach (var (entity, _) in ent.Comp.StoredItems)
        {
            if (HasComp<ScanDetectableComponent>(entity))
                args.EntityDetected = true; // Keep checking, in case there's a bypass item

            if (!TryComp<ScanByPassComponent>(entity, out var component))
                continue;

            if (component.Toggleable && !_itemToggle.IsActivated(entity))
                continue;

            if (component.Powered && !_powerReceiver.IsPowered(entity) && !_powerCell.HasDrawCharge(entity))
                continue;

            args.ByPass = true;
            break;
        }
    }

    private void OnInventoryRelayStorage(Entity<StorageComponent> ent, ref InventoryRelayedEvent<TryDetectItem> args)
    {
        if (args.Args.ByPass) // No need to check if already bypassed
            return;

        foreach (var (entity, _) in ent.Comp.StoredItems)
        {
            if (HasComp<ScanDetectableComponent>(entity))
                args.Args.EntityDetected = true;  // Keep checking, in case there's a bypass item

            if (!TryComp<ScanByPassComponent>(entity, out var component))
                continue;

            if (component.Toggleable && !_itemToggle.IsActivated(entity))
                continue;

            if (component.Powered && !_powerReceiver.IsPowered(entity) && !_powerCell.HasDrawCharge(entity))
                continue;

            args.Args.ByPass = true;
            break;
        }
    }

    private void OnHandRelayStorage(Entity<StorageComponent> ent, ref HeldRelayedEvent<TryDetectItem> args)
    {
        if (args.Args.ByPass) // No need to check if already bypassed
            return;

        foreach (var (entity, _) in ent.Comp.StoredItems)
        {
            if (HasComp<ScanDetectableComponent>(entity))
                args.Args.EntityDetected = true;  // Keep checking, in case there's a bypass item

            if (!TryComp<ScanByPassComponent>(entity, out var component))
                continue;

            if (component.Toggleable && !_itemToggle.IsActivated(entity))
                continue;

            if (component.Powered && !_powerReceiver.IsPowered(entity) && !_powerCell.HasDrawCharge(entity))
                continue;

            args.Args.ByPass = true;
            break;
        }
    }

    #endregion

    #region Bypass

    /// <summary>
    /// An entity with <see cref="ScanByPassComponent"/> is attempting to bypass scan gate detection.
    /// </summary>
    private void OnBypass(Entity<ScanByPassComponent> ent, ref TryDetectItem args)
    {
        if (ent.Comp.Toggleable && !_itemToggle.IsActivated(ent.Owner))
            return;

        if (ent.Comp.Powered && !_powerReceiver.IsPowered(ent.Owner) && !_powerCell.HasDrawCharge(ent.Owner))
            return;

        args.ByPass = true;
    }

    /// <summary>
    /// An entity with <see cref="ScanByPassComponent"/> is attempting to bypass scan gate detection.
    /// </summary>
    private void OnInventoryRelayBypass(Entity<ScanByPassComponent> ent, ref InventoryRelayedEvent<TryDetectItem> args)
    {
        if (ent.Comp.Toggleable && !_itemToggle.IsActivated(ent.Owner))
            return;

        if (ent.Comp.Powered && !_powerReceiver.IsPowered(ent.Owner) && !_powerCell.HasDrawCharge(ent.Owner))
            return;

        args.Args.ByPass = true;
    }

    /// <summary>
    /// An entity with <see cref="ScanByPassComponent"/> is attempting to bypass scan gate detection.
    /// </summary>
    private void OnHandRelayBypass(Entity<ScanByPassComponent> ent, ref HeldRelayedEvent<TryDetectItem> args)
    {
        if (ent.Comp.Toggleable && !_itemToggle.IsActivated(ent.Owner))
            return;

        if (ent.Comp.Powered && !_powerReceiver.IsPowered(ent.Owner) && !_powerCell.HasDrawCharge(ent.Owner))
            return;

        args.Args.ByPass = true;
    }

    #endregion

    #region Actions

    /// <summary>
    /// Action which is performed when an item is detected by the scan gate.
    /// </summary>
    private void ItemDetected(Entity<ScanGateComponent> ent)
    {
        _audio.PlayPvs(ent.Comp.ScanFailSound, ent.Owner); // Play fail sound, when detect something
        SetState(ent, ent.Comp.ScanFailState);
        _deviceLink.InvokePort(ent.Owner, ent.Comp.FailSignal);
    }

    /// <summary>
    /// Action which is performed when no item is detected by the scan gate.
    /// </summary>
    private void NoItemDetected(Entity<ScanGateComponent> ent)
    {
        _audio.PlayPvs(ent.Comp.ScanSound, ent.Owner); // Play scan sound
        SetState(ent, ent.Comp.ScanSuccessState);
        _deviceLink.InvokePort(ent.Owner, ent.Comp.SuccessSignal);
    }

    /// <summary>
    /// Sets the visual state of the scan gate and resets it to idle after 1 second.
    /// </summary>
    private void SetState(Entity<ScanGateComponent> ent, string state)
    {
        _appearance.SetData(ent.Owner, ScanGateVisuals.State, state);
        ent.Comp.StateResetTime = _timing.CurTime + StateResetDelay;
        Dirty(ent);
    }
    #endregion
}
