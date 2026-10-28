using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Weapons.Ranged;

/// <summary>
/// Controls access to the ammunition box through the weapon's feed cover state.
/// </summary>
public sealed partial class BeltFeedSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityQuery<ItemSlotsComponent> _itemSlotsQuery = default!;
    [Dependency] private EntityQuery<ChamberMagazineAmmoProviderComponent> _chamberQuery = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string MagazineSlot = "gun_magazine";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BeltFeedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BeltFeedComponent, BoltClosedChangedEvent>(OnBoltClosedChanged);
        SubscribeLocalEvent<BeltFeedComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<BeltFeedComponent, UseInHandEvent>(OnUseInHand, before: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<BeltFeedComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeInteract);
        SubscribeLocalEvent<BeltFeedComponent, EntInsertedIntoContainerMessage>(OnMagazineInserted);
        SubscribeLocalEvent<BeltFeedComponent, EntRemovedFromContainerMessage>(OnMagazineRemoved);
    }

    private void OnStartup(Entity<BeltFeedComponent> ent, ref ComponentStartup args)
    {
        ConfigureMagazineSlot(ent);

        if (!_chamberQuery.TryComp(ent, out var chamber) || chamber.BoltClosed == null)
            return;

        SetMagazineLock(ent, chamber.BoltClosed.Value);
        UpdateRequiresCycle(ent);
    }

    private void OnBoltClosedChanged(Entity<BeltFeedComponent> ent, ref BoltClosedChangedEvent args)
    {
        SetMagazineLock(ent, args.Closed);

        if (!args.Closed)
            SetRequiresCycle(ent, true);
    }

    private void OnAttemptShoot(Entity<BeltFeedComponent> ent, ref AttemptShootEvent args)
    {
        if (_chamberQuery.TryComp(ent, out var chamber) && chamber.BoltClosed == false)
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("belt-feed-shoot-cover-open");
            return;
        }

        if (ent.Comp.RequiresCycle)
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("belt-feed-shoot-cycle-required");
        }
    }

    private void OnUseInHand(Entity<BeltFeedComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (_chamberQuery.TryComp(ent, out var chamber) && chamber.BoltClosed == false)
        {
            args.Handled = true;
            TryCycle((ent.Owner, ent.Comp), args.User);
            return;
        }

        if (ent.Comp.RequiresCycle && TryCycle((ent.Owner, ent.Comp), args.User))
            args.Handled = true;
    }

    private void OnAlternativeInteract(Entity<BeltFeedComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract || args.Hands == null ||
            !_chamberQuery.TryComp(ent, out var chamber) || chamber.BoltClosed != true ||
            !_itemSlotsQuery.TryComp(ent, out var slots) ||
            !_itemSlots.TryGetSlot(ent, MagazineSlot, out _, slots))
        {
            return;
        }

        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("gun-chamber-bolt-open"),
            Priority = 3,
            Act = () => _gun.SetBoltClosed(ent, chamber, false, user),
        });
    }

    private void OnMagazineInserted(Entity<BeltFeedComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != MagazineSlot || _timing.ApplyingState)
            return;

        SetRequiresCycle(ent, true);
    }

    private void OnMagazineRemoved(Entity<BeltFeedComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != MagazineSlot || _timing.ApplyingState)
            return;

        SetRequiresCycle(ent, true);
    }

    /// <summary>
    /// Attempts to cycle the weapon after manipulating its belt feed.
    /// </summary>
    public bool TryCycle(Entity<BeltFeedComponent?> ent, EntityUid? user)
    {
        if (!Resolve(ent, ref ent.Comp) || !CanCycle(ent, user))
            return false;

        DoCycle((ent.Owner, ent.Comp), user);
        return true;
    }

    /// <summary>
    /// Checks whether the weapon can be cycled.
    /// </summary>
    public bool CanCycle(Entity<BeltFeedComponent?> ent, EntityUid? user, bool quiet = false)
    {
        if (!Resolve(ent, ref ent.Comp) || !_chamberQuery.TryComp(ent, out var chamber))
            return false;

        if (chamber.BoltClosed == true)
            return true;

        if (!_itemSlotsQuery.TryComp(ent, out var slots) ||
            !_itemSlots.TryGetSlot(ent, MagazineSlot, out var magazineSlot, slots))
        {
            return false;
        }

        if (magazineSlot.Item != null)
        {
            if (!quiet && user != null)
                _popup.PopupClient(Loc.GetString("belt-feed-cycle-remove-magazine"), ent, user.Value);

            return false;
        }

        if (_gun.CanEjectChamberedRound((ent.Owner, chamber)))
            return true;

        if (!quiet && user != null)
            _popup.PopupClient(Loc.GetString("belt-feed-cycle-cover-open"), ent, user.Value);

        return false;
    }

    private void DoCycle(Entity<BeltFeedComponent> ent, EntityUid? user)
    {
        var chamber = _chamberQuery.Comp(ent);

        if (chamber.BoltClosed == false)
        {
            _gun.TryEjectChamberedRound((ent.Owner, chamber), user);
            SetRequiresCycle(ent, true);
            return;
        }

        _gun.UseChamberedInternal(ent, chamber, user);
        UpdateRequiresCycle(ent);
    }

    private void UpdateRequiresCycle(Entity<BeltFeedComponent> ent)
    {
        SetRequiresCycle(ent, _gun.GetChamberEntity(ent) == null);
    }

    private void SetRequiresCycle(Entity<BeltFeedComponent> ent, bool requiresCycle)
    {
        if (ent.Comp.RequiresCycle == requiresCycle)
            return;

        ent.Comp.RequiresCycle = requiresCycle;
        Dirty(ent);
    }

    private void SetMagazineLock(Entity<BeltFeedComponent> ent, bool locked)
    {
        if (!_itemSlotsQuery.TryComp(ent, out var slots) ||
            !_itemSlots.TryGetSlot(ent, MagazineSlot, out var magazineSlot, slots))
        {
            return;
        }

        _itemSlots.SetLock(ent, magazineSlot, locked, slots);
    }

    private void ConfigureMagazineSlot(Entity<BeltFeedComponent> ent)
    {
        if (!_itemSlotsQuery.TryComp(ent, out var slots) ||
            !_itemSlots.TryGetSlot(ent, MagazineSlot, out var magazineSlot, slots))
        {
            return;
        }

        if (!magazineSlot.Swap)
            return;

        magazineSlot.Swap = false;
        Dirty(ent, slots);
    }
}
