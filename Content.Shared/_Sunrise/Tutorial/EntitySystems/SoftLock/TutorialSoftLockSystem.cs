using System;
using System.Collections.Generic;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.Events;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Item;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems.SoftLock;

/// <summary>
///     Blocks tutorial-breaking actions while a tutorial step effect is active.
/// </summary>
public sealed partial class TutorialSoftLockSystem : EntitySystem
{
    private static readonly TimeSpan PopupCooldown = TimeSpan.FromSeconds(1);

    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _lastPopupTimes = new();

    public override void Initialize()
    {
        base.Initialize();

        InitializeEquip();
        InitializeStorage();
        InitializePickup();
        InitializeInteractions();

        SubscribeLocalEvent<TutorialSoftLockTrackerComponent, DidEquipHandEvent>(OnHandEquipped);
        SubscribeLocalEvent<TutorialSoftLockTrackerComponent, ComponentShutdown>(OnTrackerShutdown);
    }
    private void OnTrackerShutdown(Entity<TutorialSoftLockTrackerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var target in ent.Comp.LinkedEntities)
        {
            if (!TryComp<TutorialSoftLockEntityComponent>(target, out var softLock) ||
                !softLock.Players.Remove(ent))
            {
                continue;
            }

            if (softLock.Players.Count == 0)
            {
                RemComp<TutorialSoftLockEntityComponent>(target);
                continue;
            }

            Dirty(target, softLock);
        }
    }

    public void ApplyStepSoftLocks(Entity<TutorialPlayerComponent> ent, TutorialStepPrototype step)
    {
        ClearStepSoftLocks(ent);

        if (!HasEntitySoftLocks(ent.Owner))
            return;

        var tracker = EnsureComp<TutorialSoftLockTrackerComponent>(ent);
        foreach (var target in _lookup.GetEntitiesInRange(ent, step.ObserveRange))
        {
            if (ShouldMarkEntity(ent.Owner, target))
                TryMarkEntity(ent.Owner, target, tracker);
        }

        MarkEquippedEntities(ent, tracker);
    }

    public void ClearStepSoftLocks(Entity<TutorialPlayerComponent> ent)
    {
        RemComp<TutorialSoftLockTrackerComponent>(ent);
    }

    private void MarkEquippedEntities(
        Entity<TutorialPlayerComponent> ent,
        TutorialSoftLockTrackerComponent tracker)
    {
        if (TryComp<HandsComponent>(ent, out var hands))
        {
            foreach (var held in _hands.EnumerateHeld((ent, hands)))
            {
                if (ShouldMarkEntity(ent.Owner, held))
                    TryMarkEntity(ent.Owner, held, tracker);
            }
        }

        if (!TryComp<InventoryComponent>(ent, out var inventory))
            return;

        foreach (var slot in inventory.Slots)
        {
            if (_inventory.TryGetSlotEntity(ent, slot.Name, out var item, inventory))
            {
                if (ShouldMarkEntity(ent.Owner, item.Value))
                    TryMarkEntity(ent.Owner, item.Value, tracker);
            }
        }
    }

    private bool HasEntitySoftLocks(EntityUid player)
    {
        foreach (var component in AllComps(player))
        {
            if (component is ITutorialEntitySoftLockComponent)
                return true;
        }

        return false;
    }

    private bool ShouldMarkEntity(EntityUid player, EntityUid target)
    {
        var ev = new TutorialShouldMarkEntityEvent(target);
        RaiseLocalEvent(player, ref ev);
        return ev.ShouldMark;
    }

    private void TryMarkEntity(
        EntityUid player,
        EntityUid target,
        TutorialSoftLockTrackerComponent tracker)
    {
        if (!tracker.LinkedEntities.Add(target))
            return;

        var softLock = EnsureComp<TutorialSoftLockEntityComponent>(target);
        if (softLock.Players.Add(player))
            Dirty(target, softLock);
    }

    private bool IsAllowedPrototype(EntityUid uid, EntProtoId prototype)
    {
        var entityPrototype = Prototype(uid);
        return entityPrototype != null && entityPrototype.ID == prototype;
    }

    private bool IsAllowedPrototype(EntityUid uid, List<EntProtoId> prototypes)
    {
        if (prototypes.Count == 0)
            return true;

        var entityPrototype = Prototype(uid);
        if (entityPrototype == null)
            return false;

        for (var i = 0; i < prototypes.Count; i++)
        {
            if (entityPrototype.ID == prototypes[i])
                return true;
        }

        return false;
    }

    private bool HasBlockedPrototype(EntityUid uid, List<EntProtoId> prototypes)
    {
        var prototype = Prototype(uid);
        if (prototype == null)
            return false;

        for (var i = 0; i < prototypes.Count; i++)
        {
            if (prototypes[i] == prototype.ID)
                return true;
        }

        return false;
    }

    private void ShowPopup(EntityUid user, LocId popup)
    {
        if (_lastPopupTimes.TryGetValue(user, out var lastPopup) &&
            _timing.CurTime - lastPopup < PopupCooldown)
        {
            return;
        }

        _lastPopupTimes[user] = _timing.CurTime;
        _popup.PopupClient(Loc.GetString(popup), user, user);
    }

    [ByRefEvent]
    private record struct TutorialShouldMarkEntityEvent(EntityUid Target)
    {
        public readonly EntityUid Target = Target;
        public bool ShouldMark;
    }
}
