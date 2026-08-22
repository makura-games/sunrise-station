using Content.Shared._Sunrise.Jump;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.SunriseStanding;

public sealed partial class SunriseStandingStateSystem : EntitySystem
{
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    private readonly EntProtoId _fallStatusEffectKey = "StatusEffectFall";
    public const float FallModifier = 0.2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CanFallComponent, KnockedDownEvent>(OnDown);
        SubscribeLocalEvent<CanFallComponent, MoveInputEvent>(OnMoveInput);
    }
    private void OnMoveInput(Entity<CanFallComponent> ent, ref MoveInputEvent args)
    {
        ent.Comp.IsMoving = args.Entity.Comp.HeldMoveButtons != MoveButtons.None;
    }
    private void OnDown(Entity<CanFallComponent> ent, ref KnockedDownEvent ev)
    {
        if (_gravity.IsWeightless(ent.Owner))
            return;

        if (!ent.Comp.IsMoving)
            return;

        Fall(ent);
    }

    public void Fall(Entity<CanFallComponent> ent)
    {
        if (HasComp<ActiveLeaperComponent>(ent))
            return;

        if (!TryComp<StaminaComponent>(ent, out var stamina))
            return;

        var threshold = stamina.CritThreshold * (1 - ent.Comp.MinimumStamina);

        if (stamina.StaminaDamage >= threshold)
        {
            _popup.PopupPredicted(Loc.GetString("cant-fall-no-stamina"), null, ent, ent);
            return;
        }

        if (_statusEffects.HasEffectComp<JumpStatusEffectComponent>(ent))
            return;

        var ev = new FallAttemptEvent();
        RaiseLocalEvent(ent, ref ev);
        if (ev.Cancelled)
            return;

        if (!TryComp<KnockedDownComponent>(ent, out var knockedDown))
            return;

        if (knockedDown.AutoStand)
            return;

        var xform = Transform(ent);
        var throwing = xform.LocalRotation.ToWorldVec() * ent.Comp.FallDistance;
        var direction = xform.Coordinates.Offset(throwing); // to make the character jump in the direction he's looking

        _throwing.TryThrow(ent, direction, ent.Comp.FallVelocity);

        _statusEffects.TryAddStatusEffectDuration(ent,
            _fallStatusEffectKey,
            ent.Comp.Duration);

        _stamina.TakeStaminaDamage(ent, stamina.CritThreshold * ent.Comp.StaminaDamage, null, ent, ent, ignoreResist: true);
    }
}
