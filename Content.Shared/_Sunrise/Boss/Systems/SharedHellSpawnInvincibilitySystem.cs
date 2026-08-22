using Content.Shared._Sunrise.Boss.Components;
using Content.Shared._Sunrise.Boss.Events;
using Content.Shared.Damage.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Boss.Systems;

public abstract partial class SharedHellSpawnInvincibilitySystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<HellSpawnInvincibilityComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HellSpawnInvincibilityComponent, HellSpawnInvincibilityActionEvent>(OnInvincibilityAction);
    }

    private void OnInit(Entity<HellSpawnInvincibilityComponent> ent, ref ComponentInit args)
    {
        if (TryComp<MovementSpeedModifierComponent>(ent.Owner, out var modifierComponent))
            ent.Comp.BaseSprintSpeed = modifierComponent.BaseSprintSpeed;
    }

    private void OnInvincibilityAction(Entity<HellSpawnInvincibilityComponent> ent,
        ref HellSpawnInvincibilityActionEvent args)
    {
        if (args.Handled)
            return;
        AddGodmode(ent.Owner);
        Timer.Spawn(3000, () => RemoveGodmode(ent.Owner));
        args.Handled = true;
    }

    private void AddGodmode(EntityUid uid, HellSpawnInvincibilityComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;
        if (!HasComp<GodmodeComponent>(uid))
        {
            AddComp<GodmodeComponent>(uid);
            if (TryComp<MovementSpeedModifierComponent>(uid, out var modifierComponent) && comp.BaseSprintSpeed != null)
            {
                _movement.ChangeBaseSpeed(uid,
                    modifierComponent.BaseWalkSpeed,
                    comp.BaseSprintSpeed.Value * 2,
                    modifierComponent.Acceleration);
            }

            var ev = new HellSpawnInvincibilityToggledEvent { Enabled = true };
            RaiseLocalEvent(uid, ev);
        }

        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void RemoveGodmode(EntityUid uid, HellSpawnInvincibilityComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;
        if (HasComp<GodmodeComponent>(uid))
        {
            RemComp<GodmodeComponent>(uid);
            if (TryComp<MovementSpeedModifierComponent>(uid, out var modifierComponent) && comp.BaseSprintSpeed != null)
            {
                _movement.ChangeBaseSpeed(uid,
                    modifierComponent.BaseWalkSpeed,
                    comp.BaseSprintSpeed.Value,
                    modifierComponent.Acceleration);
            }

            var ev = new HellSpawnInvincibilityToggledEvent { Enabled = false };
            RaiseLocalEvent(uid, ev);
        }

        _movement.RefreshMovementSpeedModifiers(uid);
    }
}

public sealed class HellSpawnInvincibilityToggledEvent : EntityEventArgs
{
    public bool Enabled;
}
