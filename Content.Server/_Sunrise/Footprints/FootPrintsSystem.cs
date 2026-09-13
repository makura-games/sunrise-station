using Content.Server.Atmos.Components;
using Content.Server.Gravity;
using Content.Shared._Sunrise.Footprints;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Content.Shared.Standing;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Content.Server.Fluids.EntitySystems;
using System.Collections.Generic;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Footprints;

/// <summary>
/// Handles creation and management of footprints left by entities as they move.
/// </summary>
public sealed partial class FootprintSystem : EntitySystem
{
    #region Dependencies

    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MapSystem _mapSystem = default!;
    [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private PuddleSystem _puddleSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    #endregion

    #region Entity Queries

    [Dependency] private EntityQuery<TransformComponent> _transformQuery = default!;
    [Dependency] private EntityQuery<AppearanceComponent> _appearanceQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<SolutionManagerComponent> _solutionQuery = default!;
    [Dependency] private EntityQuery<StandingStateComponent> _standingQuery = default!;
    [Dependency] private EntityQuery<FootprintComponent> _footprintQuery = default!;
    [Dependency] private EntityQuery<PressureProtectionComponent> _pressureQuery = default!;

    #endregion

    public const float PuddleMergeThreshold = 15f;
    public static readonly float FootsVolume = 15f;
    public static readonly float BodySurfaceVolume = 30f;

    #region Initialization
    /// <summary>
    /// Initializes the footprint system and sets up required queries and subscriptions.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FootprintEmitterComponent, ComponentStartup>(OnEmitterStartup);
        SubscribeLocalEvent<FootprintEmitterComponent, MoveEvent>(OnEntityMove);
        SubscribeLocalEvent<FootprintEmitterComponent, MapInitEvent>(OnFootprintEmitterMapInit);
        SubscribeLocalEvent<FootprintComponent, ComponentStartup>(OnFootprintStartup);
    }

    private void OnFootprintEmitterMapInit(Entity<FootprintEmitterComponent> entity, ref MapInitEvent args)
    {
        _solution.EnsureSolution(entity.Owner, entity.Comp.FootsSolutionName, out var feetSolution);
        _solution.SetCapacity(feetSolution, FixedPoint2.New(FootsVolume));

        _solution.EnsureSolution(entity.Owner, entity.Comp.BodySurfaceSolutionName, out var bodySolution);
        _solution.SetCapacity(bodySolution, FixedPoint2.New(BodySurfaceVolume));
    }

    /// <summary>
    /// Handles initialization of footprint emitter components.
    /// </summary>
    private void OnEmitterStartup(Entity<FootprintEmitterComponent> ent, ref ComponentStartup args)
    {
        // Add small random variation to step interval
        ent.Comp.WalkStepInterval = Math.Max(0f, ent.Comp.WalkStepInterval + _random.NextFloat(-0.05f, 0.05f));
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles footprint component startup and triggers the tile merge check.
    /// </summary>
    private void OnFootprintStartup(Entity<FootprintComponent> ent, ref ComponentStartup args)
    {
        MergeTileFootprints(ent);
    }

    /// <summary>
    /// Handles entity movement and creates footprints when appropriate.
    /// </summary>
    private void OnEntityMove(Entity<FootprintEmitterComponent> ent, ref MoveEvent args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        TryEmitFootprint(ent);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Attempts to emit a footprint for the given emitter.
    /// </summary>
    public bool TryEmitFootprint(Entity<FootprintEmitterComponent> ent)
    {
        if (!CanEmitFootprint(ent, out var stand, out var solComp, out var solution, out var gridUid, out var grid, out var tileRef, out var transform))
            return false;

        EmitFootprint(ent, stand, solComp, solution, gridUid, grid, tileRef, transform);
        return true;
    }

    /// <summary>
    /// Scans the tile under the footprint and merges footprints if their total volume reaches or exceeds the threshold.
    /// </summary>
    public void MergeTileFootprints(Entity<FootprintComponent> ent)
    {
        var transform = Transform(ent);
        var mapCoords = _transform.GetMapCoordinates((ent, transform));
        if (!_mapManager.TryFindGridAt(mapCoords, out var gridUid, out var grid))
            return;

        var tileRef = _mapSystem.GetTileRef((gridUid, grid), transform.Coordinates);
        var footprintsOnTile = new HashSet<Entity<FootprintComponent>>();
        _lookup.GetLocalEntitiesIntersecting(gridUid, tileRef.GridIndices, footprintsOnTile);

        var totalFootprintsVolume = 0f;
        foreach (var footprint in footprintsOnTile)
        {
            if (_solution.TryGetSolution(footprint.Owner, footprint.Comp.ContainerName, out var stepSol))
            {
                var footprintSolution = stepSol.Value.Comp.Solution;
                totalFootprintsVolume += footprintSolution.Volume.Float();
            }
        }

        if (totalFootprintsVolume >= PuddleMergeThreshold)
        {
            var emitters = new HashSet<Entity<FootprintEmitterComponent>>();
            _lookup.GetLocalEntitiesIntersecting(gridUid, tileRef.GridIndices, emitters);
            foreach (var emitter in emitters)
            {
                emitter.Comp.PuddleAbsorptionCooldownUntil = _gameTiming.CurTime + TimeSpan.FromSeconds(1.5f);
            }

            var mergedSolution = new Solution();
            foreach (var footprint in footprintsOnTile)
            {
                if (_solution.TryGetSolution(footprint.Owner, footprint.Comp.ContainerName, out var stepSol))
                {
                    mergedSolution.AddSolution(stepSol.Value.Comp.Solution, _prototype);
                }
                QueueDel(footprint.Owner);
            }

            _puddleSystem.TrySpillAt(tileRef, mergedSolution, out _, sound: false);
        }
    }

    #endregion

    #region Interaction Flow Logic

    /// <summary>
    /// Checks if a footprint can be created and resolves the emitter's state.
    /// </summary>
    public bool CanEmitFootprint(
        Entity<FootprintEmitterComponent> ent,
        out bool stand,
        out Entity<SolutionComponent> solComp,
        out Solution solution,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out TileRef tileRef,
        out TransformComponent transform)
    {
        stand = default;
        solComp = default;
        solution = default!;
        gridUid = default;
        grid = default!;
        tileRef = default;
        transform = default!;

        if (!_solutionQuery.TryComp(ent, out var container))
            return false;

        stand = !_standingQuery.TryComp(ent, out var standing) || standing.Standing;

        var solCont = (ent, container);
        if (stand)
        {
            if (!_solution.ResolveSolution(solCont, ent.Comp.FootsSolutionName, ref ent.Comp.FootsSolution, out var footsSolution))
                return false;

            solution = footsSolution;
            solComp = ent.Comp.FootsSolution.Value;
        }
        else
        {
            if (!_solution.ResolveSolution(solCont, ent.Comp.BodySurfaceSolutionName, ref ent.Comp.BodySurfaceSolution, out var bodySurfaceSolution))
                return false;

            solution = bodySurfaceSolution;
            solComp = ent.Comp.BodySurfaceSolution.Value;
        }

        if (solution.Volume <= 0)
            return false;

        if (!_physicsQuery.TryComp(ent, out var body))
            return false;

        if (body.BodyStatus == BodyStatus.InAir || _gravity.IsWeightless(ent.Owner))
            return false;

        transform = Transform(ent);
        var mapCoords = _transform.GetMapCoordinates((ent, transform));
        if (!_mapManager.TryFindGridAt(mapCoords, out gridUid, out var mapGrid))
            return false;

        grid = mapGrid;

        var distanceMoved = (transform.LocalPosition - ent.Comp.LastStepPosition).Length();
        var requiredDistance = stand ? ent.Comp.WalkStepInterval : ent.Comp.DragMarkInterval;

        if (!(distanceMoved > requiredDistance))
            return false;

        tileRef = _mapSystem.GetTileRef((gridUid, grid), transform.Coordinates);

        if (_puddleSystem.TryGetPuddle(tileRef, out _))
            return false;

        return true;
    }

    /// <summary>
    /// Emits a footprint and manages the state update.
    /// </summary>
    public void EmitFootprint(
        Entity<FootprintEmitterComponent> ent,
        bool stand,
        Entity<SolutionComponent> solComp,
        Solution solution,
        EntityUid gridUid,
        MapGridComponent grid,
        TileRef tileRef,
        TransformComponent transform)
    {
        ent.Comp.IsRightStep = !ent.Comp.IsRightStep;

        var footprintEntity = SpawnFootprint(gridUid, ent.Comp, solution, ent, transform, stand);

        UpdateFootprint(footprintEntity, (ent, ent.Comp), solComp, transform, stand);

        UpdateEmitterState(ent.Comp, transform);
    }

    #endregion

    #region Footprint Creation and Management

    /// <summary>
    /// Creates a new footprint entity at the calculated position.
    /// </summary>
    private EntityUid SpawnFootprint(
        EntityUid gridUid,
        FootprintEmitterComponent emitter,
        Solution emitterSolution,
        EntityUid emitterOwner,
        TransformComponent transform,
        bool stand)
    {
        var coords = CalculateFootprintPosition(gridUid, emitter, transform, stand);
        var entity = Spawn(stand ? emitter.FootprintPrototype : emitter.DragMarkPrototype, coords);

        var appearance = UpdateAppearance(entity, emitterSolution);

        if (appearance == null)
            return entity;

        var visualType = DetermineVisualState(emitterOwner, stand);
        _appearance.SetData(entity,
            FootprintVisualParameter.VisualState,
            GetStateId(visualType, emitter),
            appearance);

        return entity;
    }

    private AppearanceComponent? UpdateAppearance(EntityUid entity, Solution emitterSolution)
    {
        if (!_appearanceQuery.TryComp(entity, out var appearance))
            return null;

        var t = emitterSolution.Volume.Float() / emitterSolution.MaxVolume.Float();
        t = Easings.OutQuad(t);
        t = MathF.Pow(t, 0.7f);
        var alpha = Math.Clamp(0.05f + t * 0.7f, 0f, 1f);

        _appearance.SetData(entity,
            FootprintVisualParameter.TrackColor,
            emitterSolution.GetColor(_prototype).WithAlpha(alpha),
            appearance);

        return appearance;
    }

    private string GetStateId(FootprintVisualType visualType, FootprintEmitterComponent emitter)
    {
        return visualType switch
        {
            FootprintVisualType.BareFootprint => emitter.IsRightStep
                ? _random.Pick(emitter.RightBareFootState)
                : _random.Pick(emitter.LeftBareFootState),

            FootprintVisualType.ShoeFootprint => _random.Pick(emitter.ShoeFootState),
            FootprintVisualType.SuitFootprint => _random.Pick(emitter.PressureSuitFootState),
            FootprintVisualType.DragMark => _random.Pick(emitter.DraggingStates),

            _ => throw new InvalidOperationException($"Unknown footprint visual type: {visualType}")
        };
    }

    /// <summary>
    /// Updates footprint rotation and reagent transfer.
    /// </summary>
    private void UpdateFootprint(
        EntityUid footprintEntity,
        Entity<FootprintEmitterComponent> emitter,
        Entity<SolutionComponent> emitterEntSolution,
        TransformComponent transform,
        bool stand)
    {
        if (!_transformQuery.TryComp(footprintEntity, out var footprintTransform))
            return;

        footprintTransform.LocalRotation = stand
            ? transform.LocalRotation + Angle.FromDegrees(180f)
            : (transform.LocalPosition - emitter.Comp.LastStepPosition).ToAngle() + Angle.FromDegrees(-90f);

        TransferReagents(footprintEntity, emitter, emitterEntSolution, stand);
    }
    #endregion

    #region State Management

    /// <summary>
    /// Updates emitter state after creating a footprint.
    /// </summary>
    private void UpdateEmitterState(FootprintEmitterComponent emitter, TransformComponent transform)
    {
        emitter.LastStepPosition = transform.LocalPosition;
    }

    /// <summary>
    /// Transfers reagents from emitter to footprint if applicable.
    /// </summary>
    private void TransferReagents(EntityUid footprintEntity, Entity<FootprintEmitterComponent> emitter, Entity<SolutionComponent> emitterSolution, bool stand)
    {
        if (!_footprintQuery.TryComp(footprintEntity, out var footprint)
            || !_solution.ResolveSolution(footprintEntity,
                footprint.ContainerName,
                ref footprint.SolutionContainer,
                out _))
            return;

        var splitSolution = _solution.SplitSolution(emitterSolution, stand ? emitter.Comp.TransferVolumeFoot : emitter.Comp.TransferVolumeDragMark);

        _solution.AddSolution(footprint.SolutionContainer.Value, splitSolution);
    }
    #endregion

    #region Utility Methods

    /// <summary>
    /// Calculates the position where a footprint should be placed.
    /// </summary>
    private EntityCoordinates CalculateFootprintPosition(
        EntityUid uid,
        FootprintEmitterComponent emitter,
        TransformComponent transform,
        bool stand)
    {
        if (!stand)
            return new EntityCoordinates(uid, transform.LocalPosition);

        var offset = emitter.IsRightStep
            ? new Angle(Angle.FromDegrees(180f) + transform.LocalRotation)
                .RotateVec(emitter.PlacementOffset)
            : new Angle(transform.LocalRotation).RotateVec(emitter.PlacementOffset);

        return new EntityCoordinates(uid, transform.LocalPosition + offset);

    }

    /// <summary>
    /// Determines the visual state for a footprint based on entity equipment.
    /// </summary>
    private FootprintVisualType DetermineVisualState(EntityUid uid, bool stand)
    {
        if (!stand)
            return FootprintVisualType.DragMark;

        var state = FootprintVisualType.BareFootprint;

        if (_inventory.TryGetSlotEntity(uid, "shoes", out _))
            state = FootprintVisualType.ShoeFootprint;

        if (_inventory.TryGetSlotEntity(uid, "outerClothing", out var suit)
            && _pressureQuery.TryComp(suit, out _))
            state = FootprintVisualType.SuitFootprint;

        return state;
    }
    #endregion
}
