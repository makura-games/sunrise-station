using System.Numerics;
using Content.Server._Sunrise.Boss.Components;
using Content.Shared._Sunrise.Boss.Events;
using Content.Shared.Actions;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._Sunrise.Boss.Systems;

/// <summary>
///     Система для обработки способности Rush у босса
/// </summary>
public sealed partial class HellSpawnRushSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<HellSpawnRushComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<HellSpawnRushComponent, HellSpawnRushActionEvent>(OnRush);
        SubscribeLocalEvent<HellSpawnRushComponent, ThrowDoHitEvent>(OnThrowHit);
        SubscribeLocalEvent<HellSpawnRushComponent, LandEvent>(OnLand);
    }

    private void OnInit(Entity<HellSpawnRushComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.RushActionEntity, ent.Comp.RushAction);
    }

    private void OnRush(Entity<HellSpawnRushComponent> ent, ref HellSpawnRushActionEvent args)
    {
        if (_container.IsEntityOrParentInContainer(ent.Owner) || args.Handled)
            return;

        Rush(args.Performer, args.Target, ent.Comp.Range);
        args.Handled = true;

        // ent.Comp.IsThrown = true;
    }

    private void OnLand(Entity<HellSpawnRushComponent> ent, ref LandEvent args)
    {
        // ent.Comp.IsThrown = false;
        if (ent.Comp.DoCameraKickOnLand)
            KickCameras(Transform(ent).Coordinates, ent.Comp.CameraKickRange, ent.Comp.CameraKickback);

        var query = _lookup.GetEntitiesInRange<DamageableComponent>(Transform(ent.Owner).Coordinates, 1.3f);
        foreach (var entity in query)
        {
            if (_whitelist.IsWhitelistFail(ent.Comp.Blacklist, entity))
                continue;
            _damageable.TryChangeDamage(entity.Owner, ent.Comp.ThrowHitDamageDict);
        }

        QueueDel(ent.Comp.RuneUid);
    }

    public void KickCameras(EntityCoordinates coordinates, float range, Vector2 kickback)
    {
        foreach (var camera in _lookup.GetEntitiesInRange<EyeComponent>(coordinates, range))
        {
            _recoil.KickCamera(camera, kickback);
        }
    }

    public void Rush(EntityUid uid, EntityCoordinates target, float? maxDistance = null, HellSpawnRushComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var xform = Transform(uid);
        var direction = target.ToMap(EntityManager, _transform).Position - xform.MapPosition.Position;

        if (maxDistance != null && direction.Length() > maxDistance)
            direction = direction.Normalized() * maxDistance.Value;

        _throwing.TryThrow(uid, direction, 7f, uid, 10f);
        QueueDel(component.RuneUid);
        component.RuneUid = Spawn("HellSpawnRushRune",
            new EntityCoordinates(xform.Coordinates.EntityId, xform.Coordinates.Position + target.Position - xform.LocalPosition));
    }

    private void OnThrowHit(EntityUid uid, HellSpawnRushComponent component, ThrowDoHitEvent args)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageableComponent))
            return;

        _damageable.TryChangeDamage(args.Target, component.ThrowHitDamageDict);
    }
}
