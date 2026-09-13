using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.AssaultOps.Icarus;

public sealed partial class IcarusBeamSystem : EntitySystem
{
    [Dependency] private IMapManager _mapMan = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private MapSystem _map = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Sunrise edit start - continuously set velocity to prevent slowdown from damping/friction
        var query = EntityQueryEnumerator<IcarusBeamComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform, out var phys))
        {
            _physics.SetLinearVelocity(uid, _transform.GetWorldRotation(uid).ToWorldVec() * comp.Speed, body: phys);

            DestroyEntities(uid, comp, xform);
            BurnEntities(uid, comp, xform);

            if (comp.DestroyTiles)
                DestroyTiles((uid, comp));

            if (_timing.CurTime > comp.LifetimeEnd)
                QueueDel(uid);
        }
        // Sunrise edit end
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IcarusBeamComponent, ComponentInit>(OnComponentInit);
    }

    private void OnComponentInit(EntityUid uid, IcarusBeamComponent component, ComponentInit args)
    {
        component.LifetimeEnd = _timing.CurTime + component.Lifetime;
        if (!TryComp(uid, out PhysicsComponent? phys))
            return;
        _physics.SetLinearDamping(uid, phys, 0f);
        _physics.SetFriction(uid, phys, 0f);
        _physics.SetAngularDamping(uid, phys, 0f);
    }

    public void LaunchInDirection(EntityUid uid, Vector2 dir, IcarusBeamComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;


        if (TryComp(uid, out PhysicsComponent? phys))
        {
            var impulseVector = dir.Normalized() * comp.Speed * phys.Mass;

            _physics.ApplyLinearImpulse(uid, impulseVector, body: phys);
            _transform.SetWorldRotation(uid, impulseVector.ToWorldAngle());
        }
    }

    /// <summary>
    /// Destroy any grid tiles in beam radius.
    /// </summary>
    private void DestroyTiles(Entity<IcarusBeamComponent> ent)
    {
        var radius = ent.Comp.DestroyRadius;
        var worldPos = _transform.GetWorldPosition(ent);

        var circle = new Circle(worldPos, radius);
        var r = new Vector2(radius, radius);
        var box = new Box2(worldPos - r, worldPos + r);

        var grids = new List<Entity<MapGridComponent>>();
        _mapMan.FindGridsIntersecting(Transform(ent).MapID, box, ref grids);

        foreach (var grid in grids)
        {
            // Bundle these together so we can use the faster helper to set tiles.
            var toDestroy = new List<(Vector2i, Tile)>();

            foreach (var tile in _map.GetTilesIntersecting(grid, grid.Comp, circle))
            {
                if (tile.Tile.IsEmpty)
                    continue;

                toDestroy.Add((tile.GridIndices, Tile.Empty));
            }

            _map.SetTiles(grid, grid.Comp, toDestroy);
        }
    }

    /// <summary>
    /// Handle deleting entities in beam radius.
    /// </summary>
    private void DestroyEntities(EntityUid beam, IcarusBeamComponent component, TransformComponent trans)
    {
        var radius = component.DestroyRadius - 0.5f;
        var entitys = _lookup.GetEntitiesInRange(trans.MapID, trans.WorldPosition, radius);
        foreach (var entity in entitys)
        {
            if (!CanDestroy(beam, component, entity))
                continue;

            QueueDel(entity);
        }
    }

    /// <summary>
    /// Handle igniting flammable entities in beam radius.
    /// </summary>
    private void BurnEntities(EntityUid beam, IcarusBeamComponent component, TransformComponent trans)
    {
        var radius = component.FlameRadius * 2;
        foreach (var entity in _lookup.GetEntitiesInRange(trans.MapID, trans.WorldPosition, radius))
        {
            if (!CanDestroy(beam, component, entity))
                continue;

            if (!TryComp<FlammableComponent>(entity, out var flammable))
                continue;

            flammable.FireStacks += 1;
            if (!flammable.OnFire)
                _flammable.Ignite(entity, beam);
        }
    }

    private bool CanDestroy(EntityUid beam, IcarusBeamComponent component, EntityUid entity)
    {
        if (entity == beam)
            return false;

        if (HasComp<MapGridComponent>(entity))
            return false;

        var current = entity;
        while (current.IsValid())
        {
            if (HasComp<GhostComponent>(current))
                return false;

            var xform = Transform(current);
            if (xform.ParentUid == current)
                break;
            current = xform.ParentUid;
        }

        return true;
    }
}
