using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.BloodCult.Systems;

public sealed partial class CultMemberSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultMemberComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnAttackAttempt(EntityUid uid, CultMemberComponent component, AttackAttemptEvent args)
    {
        if (args.Target == null)
            return;

        // If we would do zero damage, it should be fine.
        if (args.Weapon != null && args.Weapon.Value.Comp.Damage.GetTotal() == FixedPoint2.Zero)
            return;

        if (!HasComp<CultMemberComponent>(args.Target))
            return;

        ShowPopup((uid, component), args.Target.Value, component.Reason);
        args.Cancel();
    }

    private void ShowPopup(Entity<CultMemberComponent> user, EntityUid target, string reason)
    {
        // Popup logic.
        // Cooldown is needed because the input events for melee/shooting etc. will fire continuously
        if (target == user.Comp.LastAttackedEntity
            && !(_timing.CurTime > user.Comp.NextPopupTime))
            return;

        _popup.PopupClient(Loc.GetString(reason, ("entity", target)), user, user);
        user.Comp.NextPopupTime = _timing.CurTime + user.Comp.PopupCooldown;
        user.Comp.LastAttackedEntity = target;
    }
}
