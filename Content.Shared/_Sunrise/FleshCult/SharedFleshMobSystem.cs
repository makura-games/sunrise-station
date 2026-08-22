using Content.Shared.Flesh;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.FleshCult;

public sealed partial class SharedFleshMobSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FleshMobComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnAttackAttempt(Entity<FleshMobComponent> fleshMob, ref AttackAttemptEvent args)
    {
        if (args.Cancelled || args.Target == null)
            return;

        if (HasComp<FleshMobComponent>(args.Target))
        {
            ShowPopup(fleshMob, args.Target.Value, Loc.GetString("flesh-mob-cant-atack-flesh-mob"));
            args.Cancel();
        }

        if (HasComp<FleshCultistComponent>(args.Target))
        {
            ShowPopup(fleshMob, args.Target.Value, Loc.GetString("flesh-mob-cant-atack-flesh-cultist"));
            args.Cancel();
        }

        if (HasComp<FleshHeartComponent>(args.Target))
        {
            ShowPopup(fleshMob, args.Target.Value, Loc.GetString("flesh-mob-cant-atack-flesh-heart"));
            args.Cancel();
        }
    }

    private void ShowPopup(Entity<FleshMobComponent> user, EntityUid target, string reason)
    {
        if (target == user.Comp.LastAttackedEntity
            && !(_timing.CurTime > user.Comp.NextPopupTime))
            return;

        var targetName = Identity.Entity(target, EntityManager);
        _popup.PopupCursor(Loc.GetString(reason, ("entity", targetName)), user, PopupType.LargeCaution);
        user.Comp.NextPopupTime = _timing.CurTime + user.Comp.PopupCooldown;
        user.Comp.LastAttackedEntity = target;
    }

}
