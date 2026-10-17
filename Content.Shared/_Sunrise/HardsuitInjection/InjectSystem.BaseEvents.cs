using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Utility;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared._Sunrise.HardsuitInjection.Components;
using Content.Shared.Clothing.EntitySystems;

namespace Content.Shared._Sunrise.HardsuitInjection.EntitySystems;

public sealed partial class InjectSystem
{
    private void InitializeBaseEvents()
    {
        SubscribeLocalEvent<InjectComponent, ComponentInit>(OnInit);

        SubscribeLocalEvent<InjectComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<InjectComponent, EjectionEvent>(OnEject);
        SubscribeLocalEvent<InjectComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(OnGetRelayedVerbs);

        SubscribeLocalEvent<AmpulaComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<InjectNeedComponent, MobStateChangedEvent>(OnStateChanged);
    }

    private void OnInit(EntityUid uid, InjectComponent component, ComponentInit args)
    {
        component.Container = _containerSystem.EnsureContainer<ContainerSlot>(uid, component.ContainerId);

        if (!TryComp<ItemSlotsComponent>(uid, out var comp)) return;

        _itemSlotsSystem.SetLock((uid, comp), component.ContainerId, component.Locked);
    }

    private void OnExamine(EntityUid uid, InjectComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("hardsuitinjection-" + component.Locked.ToString()));
    }

    private void OnGetRelayedVerbs(EntityUid uid, InjectComponent component, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        OnGetVerbs(uid, component, args.Args);
    }

    private void OnEject(EntityUid uid, InjectComponent component, EjectionEvent args)
    {
        if (args.Handled) return;
        if (_netManager.IsClient) return;

        if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots)) return;
        if (!_itemSlotsSystem.TryGetSlot((uid, itemSlots), component.ContainerId, out var slot)) return;

        if (slot.Locked)
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuitinjection-closed"), args.Performer, args.Performer);

            return;
        }
        if (slot.ContainerSlot == null || slot.ContainerSlot.ContainedEntity == null)
        {
            _popupSystem.PopupEntity(Loc.GetString("hardsuitinjection-nobeaker"), args.Performer, args.Performer);

            return;
        }

        _itemSlotsSystem.TryEjectToHands(args.Performer, slot, args.Performer);
    }

    #region Crit

    private void OnStateChanged(EntityUid uid, InjectNeedComponent component, MobStateChangedEvent args)
    {
        if (_netManager.IsClient) return;
        if (args.NewMobState != MobState.Critical) return;

        if (!TryComp<InventoryComponent>(args.Target, out var inventory)) return;

        if (!_inventorySystem.TryGetSlotEntity(args.Target, "outerClothing", out var slot, inventory)) return;

        if (!TryComp<ItemSlotsComponent>(slot, out var _)) return;
        if (!TryComp<InjectComponent>(slot, out var _)) return;

        _popupSystem.PopupEntity(Loc.GetString("hardsuitinjection-critical"), args.Target, PopupType.Medium);

        Inject(slot.Value, slot.Value);
    }

    private void OnInserted(EntityUid uid, AmpulaComponent component, EntGotInsertedIntoContainerMessage args)
    {
        if (!TryComp<InjectComponent>(args.Container.Owner, out var inject)) return;
        var action = _actionsSystem.GetAction(inject.ToggleInjectionActionEntity);

        if (
            action == null ||
            action.Value.Comp.AttachedEntity == null
        ) return;

        if (!TryComp<MobStateComponent>(action.Value.Comp.AttachedEntity, out var state)) return;
        if (state.CurrentState == MobState.Invalid || state.CurrentState == MobState.Alive) return;

        _popupSystem.PopupEntity(Loc.GetString("hardsuitinjection-critical"), args.Container.Owner, PopupType.Medium);
        Inject(args.Container.Owner, uid);
    }

    #endregion
}
