using Content.Shared._Sunrise.Mech.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Sunrise.Mech.Systems;

/// <summary>
/// Applies the installed battery's speed modifiers to the mech.
/// </summary>
public sealed class MechSpeedModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
    }

    private void OnRefreshMovementSpeed(Entity<MechComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var battery = ent.Comp.BatterySlot.ContainedEntity;
        if (battery == null)
            return;

        if (!TryComp<MechSpeedModifierComponent>(battery.Value, out var modifier))
            return;

        args.ModifySpeed(modifier.WalkModifier, modifier.SprintModifier);
    }
}
