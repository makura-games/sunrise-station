using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

/// <summary>
/// Замедляет движение голодающего вампира
/// </summary>
public sealed class SharedVampireStarvationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
    }

    private void OnRefreshMovespeed(EntityUid uid, VampireComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<VampireBloodDrinkerComponent>(uid, out var drinker))
            return;

        if (drinker.BloodFullness > 0f)
            return;

        args.ModifySpeed(drinker.StarvationWalkSpeedModifier, drinker.StarvationSprintSpeedModifier);
    }
}
