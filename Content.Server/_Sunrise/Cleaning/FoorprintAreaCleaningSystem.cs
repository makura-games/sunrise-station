using System.Linq;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Sunrise.Footprints;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Gravity;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server._Sunrise.Cleaning;

public sealed partial class FoorprintAreaCleaningSystem : EntitySystem
{
    #region Entity Queries
    [Dependency] private EntityQuery<TransformComponent> _transformQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    #endregion

    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private AbsorbentSystem _absorbentSystem = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FootprintAreaCleanerComponent, MoveEvent>(OnEntityMove);
    }

    private void OnEntityMove(EntityUid uid, FootprintAreaCleanerComponent cleaner, ref MoveEvent args)
    {
        if (!TryComp<AbsorbentComponent>(uid, out var absorbent)
            || !_transformQuery.TryComp(uid, out var transform)
            || !_physicsQuery.TryGetComponent(uid, out var body)
            || body.BodyStatus == BodyStatus.InAir
            || _gravity.IsWeightless(uid)
            || !_mapManager.TryFindGridAt(_transformSystem.GetMapCoordinates((uid, transform)), out var gridUid, out var grid))
            return;

        var distanceMoved = (transform.LocalPosition - cleaner.LastStepPosition).Length();
        var requiredDistance = cleaner.Interval;

        if (!(distanceMoved > requiredDistance))
            return;

        var tileRef = _mapSystem.GetTileRef((gridUid, grid), transform.Coordinates);

        var puddles = new HashSet<Entity<PuddleComponent>>();
        _lookup.GetLocalEntitiesIntersecting(gridUid, tileRef.GridIndices, puddles, 0);

        if (puddles.Count > 0)
        {
            _absorbentSystem.Mop(uid, puddles.First(), uid, absorbent);
            cleaner.LastStepPosition = transform.LocalPosition;
            return;
        }

        var footPrints = new HashSet<Entity<FootprintComponent>>();
        _lookup.GetLocalEntitiesIntersecting(gridUid, tileRef.GridIndices, footPrints);

        if (footPrints.Count > 0)
        {
            if (!_solutionContainerSystem.TryGetSolution(uid, absorbent.SolutionName, out var absorberSoln))
                return;

            var tileCenterPos = _mapSystem.GridTileToLocal(gridUid, grid, tileRef.GridIndices);
            _absorbentSystem.CleanFootprints(uid, uid, absorbent, absorberSoln.Value, footPrints, tileCenterPos);
            cleaner.LastStepPosition = transform.LocalPosition;
        }
    }
}
