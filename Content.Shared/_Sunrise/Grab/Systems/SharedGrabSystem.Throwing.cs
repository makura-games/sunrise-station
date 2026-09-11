using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared._Sunrise.Grab.Components;
using Content.Shared.Throwing;
using Robust.Shared.Player;

namespace Content.Shared._Sunrise.Grab.Systems;

public sealed partial class SharedGrabSystem
{
    private void OnBeforeThrow(Entity<GrabberComponent> ent, ref BeforeThrowEvent args)
    {
        if (args.Cancelled ||
            ent.Comp.Grabbed is not { } grabbed ||
            ent.Comp.Stage < GrabStage.Hard)
        {
            return;
        }

        if (!_virtualQuery.TryComp(args.ItemUid, out var virtualItem) || virtualItem.BlockingEntity != grabbed)
            return;

        if (!_pullableQuery.TryComp(grabbed, out var pullable))
        {
            args.Cancelled = true;
            return;
        }

        if (!_pulling.TryStopPull(grabbed, pullable))
        {
            args.Cancelled = true;
            return;
        }

        var thrown = EnsureComp<GrabThrownComponent>(grabbed);
        var damage = ent.Comp.ThrowDamage * ent.Comp.ThrowDamageModifier;
        thrown.DamageOnCollide = new DamageSpecifier(damage);
        thrown.WallDamageOnCollide = new DamageSpecifier(damage);
        thrown.StaminaDamageOnCollide = ent.Comp.ThrowStaminaDamage;
        Dirty(grabbed, thrown);

        args.ItemUid = grabbed;
        args.ThrowSpeed = ent.Comp.ThrowSpeed;
    }

    private void OnGrabThrowHit(Entity<GrabThrownComponent> ent, ref ThrowDoHitEvent args)
    {
        if (ent.Comp.HasCollided)
            return;

        ent.Comp.HasCollided = true;
        Dirty(ent);

        if (!_physicsQuery.TryComp(ent.Owner, out _))
        {
            RemCompDeferred<GrabThrownComponent>(ent.Owner);
            return;
        }

        if (ent.Comp.StaminaDamageOnCollide > 0f)
            _stamina.TakeStaminaDamage(ent.Owner, ent.Comp.StaminaDamageOnCollide);

        if (ent.Comp.DamageOnCollide != null)
            _damageable.TryChangeDamage(ent.Owner, ent.Comp.DamageOnCollide);

        if (ent.Comp.WallDamageOnCollide != null)
            _damageable.TryChangeDamage(args.Target, ent.Comp.WallDamageOnCollide, origin: ent.Owner);

        _color.RaiseEffect(Color.Red, new List<EntityUid> { ent.Owner }, Filter.Pvs(ent.Owner, entityManager: EntityManager));
        RemCompDeferred<GrabThrownComponent>(ent.Owner);
    }

    private void OnGrabStopThrow(Entity<GrabThrownComponent> ent, ref StopThrowEvent args)
    {
        RemCompDeferred<GrabThrownComponent>(ent.Owner);
    }
}
