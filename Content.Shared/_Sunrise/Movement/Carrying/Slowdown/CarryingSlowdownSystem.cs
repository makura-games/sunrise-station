using Content.Shared.Movement.Systems;
using JetBrains.Annotations;

namespace Content.Shared._Sunrise.Movement.Carrying.Slowdown;

public sealed partial class CarryingSlowdownSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CarryingSlowdownComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<CarryingSlowdownComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnRefreshMoveSpeed(Entity<CarryingSlowdownComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.WalkModifier, ent.Comp.SprintModifier);
    }

    private void OnAfterAutoHandleState(Entity<CarryingSlowdownComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(ent);
    }

    /// <summary>
    /// Sets the movement speed modifiers for carrying slowdown.
    /// </summary>
    /// <param name="uid">Entity that will receive the slowdown.</param>
    /// <param name="walkSpeedModifier">Modifier for walking speed.</param>
    /// <param name="sprintSpeedModifier">Modifier for sprinting speed.</param>
    [PublicAPI]
    public void SetModifier(EntityUid uid, float walkSpeedModifier = 1f, float sprintSpeedModifier = 1f)
    {
        if (MathHelper.CloseTo(walkSpeedModifier, 1f) && MathHelper.CloseTo(sprintSpeedModifier, 1f))
        {
            RemComp<CarryingSlowdownComponent>(uid);
            _movementSpeed.RefreshMovementSpeedModifiers(uid);
            return;
        }

        var comp = EnsureComp<CarryingSlowdownComponent>(uid);
        comp.WalkModifier = walkSpeedModifier;
        comp.SprintModifier = sprintSpeedModifier;
        Dirty(uid, comp);

        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }
}
