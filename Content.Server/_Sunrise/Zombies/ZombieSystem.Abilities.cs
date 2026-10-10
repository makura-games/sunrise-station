using Content.Server.Pinpointer;
using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Zombies;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Namespace не соответствует расположению файла
namespace Content.Server.Zombies;

public sealed partial class ZombieSystem
{

    private void InitializeSunrise()
    {
        SubscribeLocalEvent<ZombieComponent, ComponentStartup>(OnSunriseStartup);
        SubscribeLocalEvent<ZombieComponent, ZombieJumpActionEvent>(OnJump);
        SubscribeLocalEvent<ZombieComponent, ZombieFlairActionEvent>(OnFlair);
        SubscribeLocalEvent<ZombieComponent, ThrowDoHitEvent>(OnThrowDoHit);
    }
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private void OnSunriseStartup(Entity<ZombieComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ent.Comp.JumpAction);
        _actions.AddAction(ent, ent.Comp.FlairAction);
    }

    private void OnThrowDoHit(Entity<ZombieComponent> ent, ref ThrowDoHitEvent args)
    {
        if (_mobState.IsDead(ent) ||
            HasComp<ZombieComponent>(args.Target) ||
            HasComp<PendingZombieComponent>(args.Target) ||
            !_mobState.IsAlive(args.Target))
        {
            return;
        }

        _stun.TryAddParalyzeDuration(args.Target, ent.Comp.ParalyzeTime);
        _damageable.TryChangeDamage(args.Target, ent.Comp.ThrowDamage, origin: args.Thrown);
    }

    private void OnFlair(Entity<ZombieComponent> ent, ref ZombieFlairActionEvent args)
    {
        if (args.Handled)
            return;

        var zombieCoordinates = _transform.GetMapCoordinates(ent);
        EntityUid? nearest = null;
        var nearestDistance = float.MaxValue;
        var query = AllEntityQuery<HumanoidProfileComponent>();

        while (query.MoveNext(out var target, out _))
        {
            if (HasComp<ZombieComponent>(target) || HasComp<ZombieImmuneComponent>(target))
                continue;

            var targetCoordinates = _transform.GetMapCoordinates(target);
            if (targetCoordinates.MapId != zombieCoordinates.MapId)
                continue;

            var distance = (targetCoordinates.Position - zombieCoordinates.Position).Length();
            if (distance > ent.Comp.MaxFlairDistance || distance >= nearestDistance)
                continue;

            nearest = target;
            nearestDistance = distance;
        }

        var message = nearest == null
            ? "Ближайших выживших не найдено."
            : $"Ближайший выживший находится {FormattedMessage.RemoveMarkupPermissive(_navMap.GetNearestBeaconString(nearest.Value))}";

        _popup.PopupEntity(message, ent, ent, PopupType.LargeCaution);
        args.Handled = true;
    }

    private void OnJump(Entity<ZombieComponent> ent, ref ZombieJumpActionEvent args)
    {
        if (args.Handled)
            return;

        var mapCoordinates = args.Target.ToMap(EntityManager, _transform);
        var direction = mapCoordinates.Position - Transform(ent).MapPosition.Position;
        if (direction.Length() > ent.Comp.MaxJumpDistance)
            direction = direction.Normalized() * ent.Comp.MaxJumpDistance;

        args.Handled = true;
        _throwing.TryThrow(ent, direction, 7f, ent, 10f);
        _chat.TryEmoteWithChat(ent, "ZombieGroan");
    }
}
