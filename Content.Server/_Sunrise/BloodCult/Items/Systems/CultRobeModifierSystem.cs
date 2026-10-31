using Content.Server._Sunrise.BloodCult.Items.Components;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;

namespace Content.Server._Sunrise.BloodCult.Items.Systems;

public sealed partial class CultRobeModifierSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultRobeModifierComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<CultRobeModifierComponent, GotUnequippedEvent>(OnUnequip);
    }

    private void OnEquip(EntityUid uid, CultRobeModifierComponent component, GotEquippedEvent args)
    {
        if (!HasComp<BloodCultistComponent>(args.EquipTarget))
            return;

        if (args.Slot != "outerClothing")
            return;

        ModifySpeed(args.EquipTarget, component, true);
        ModifyDamage(args.EquipTarget, component, true);
    }

    private void OnUnequip(EntityUid uid, CultRobeModifierComponent component, GotUnequippedEvent args)
    {
        if (!HasComp<BloodCultistComponent>(args.EquipTarget))
            return;

        if (args.Slot != "outerClothing")
            return;

        ModifySpeed(args.EquipTarget, component, false);
        ModifyDamage(args.EquipTarget, component, false);
    }

    private void ModifySpeed(EntityUid uid, CultRobeModifierComponent comp, bool increase)
    {
        if (!TryComp<MovementSpeedModifierComponent>(uid, out var move))
            return;

        var walkSpeed = increase ? move.BaseWalkSpeed * comp.SpeedModifier : move.BaseWalkSpeed / comp.SpeedModifier;

        var sprintSpeed =
            increase ? move.BaseSprintSpeed * comp.SpeedModifier : move.BaseSprintSpeed / comp.SpeedModifier;

        _movement.ChangeBaseSpeed(uid, walkSpeed, sprintSpeed, move.Acceleration, move);
    }

    private void ModifyDamage(EntityUid uid, CultRobeModifierComponent comp, bool increase)
    {
        string? damageSet = null;
        if (increase)
        {
            if (!TryComp<DamageableComponent>(uid, out var damage))
                return;

            comp.StoredDamageSetId = damage.DamageModifierSetId;
            damageSet = comp.DamageModifierSetId;
        }
        else
        {
            if (comp.StoredDamageSetId != null)
                damageSet = comp.StoredDamageSetId;

            comp.StoredDamageSetId = null;
        }

        _damageable.SetDamageModifierSetId(uid, damageSet);
    }
}
