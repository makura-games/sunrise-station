using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Popups;

namespace Content.Shared._Sunrise.Mech.Equipment.EntitySystems;

public sealed partial class SharedMechEquipmentSelectSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MechComponent, MechToggleEquipmentEvent>(OnSelectEquipmentAction);
        Subs.BuiEvents<MechComponent>(MechEquipmentSelectUiKey.Key, subs => subs.Event<MechActiveEquipmentSelectMessage>(OnRadialSelected));
    }

    private void OnSelectEquipmentAction(EntityUid uid, MechComponent comp, MechToggleEquipmentEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<UserInterfaceComponent>(uid, out var uiComp))
            return;

        _ui.TryOpenUi((uid, uiComp), MechEquipmentSelectUiKey.Key, args.Performer);

        args.Handled = true;
    }

    private void OnRadialSelected(EntityUid uid, MechComponent comp, MechActiveEquipmentSelectMessage msg)
    {
        if (msg.Actor != comp.PilotSlot.ContainedEntity)
            return;

        var equipment = GetEntity(msg.SelectedEquipment);

        if (equipment.HasValue &&
            (!HasComp<MechEquipmentComponent>(equipment.Value) || !comp.EquipmentContainer.Contains(equipment.Value)))
            return;

        comp.CurrentSelectedEquipment = equipment;

        var popupString = comp.CurrentSelectedEquipment is not null
            ? Loc.GetString("mech-equipment-select-popup", ("item", comp.CurrentSelectedEquipment))
            : Loc.GetString("mech-equipment-select-none-popup");

        _popup.PopupPredicted(popupString, uid, comp.PilotSlot.ContainedEntity);
        Dirty(uid, comp);
    }
}
