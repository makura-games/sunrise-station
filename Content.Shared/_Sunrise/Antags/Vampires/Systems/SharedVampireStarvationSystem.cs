using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

/// <summary>
/// Applies movement slowdown when a vampire is starving
/// </summary>
public sealed class SharedVampireStarvationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
    }

    private void OnRefreshMovespeed(Entity<VampireComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.BloodFullness > 0f)
            return;

        args.ModifySpeed(ent.Comp.StarvationWalkSpeedModifier, ent.Comp.StarvationSprintSpeedModifier);
    }
}
