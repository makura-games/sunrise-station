using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server._Sunrise.Shuttles.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Atmos.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Timing;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

#pragma warning disable IDE0130 // Пространство имён vanilla-системы сохраняется для partial-расширения.
namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    /*
     * Расширение Sunrise для резервирования доков, разведения шаттлов и физики FTL.
     */
    private const float SunriseFtlThrowForce = 20f;
    private const float SunriseArrivalsFtlOffset = 10000f;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;


    private void ClearSunriseFtlReservations(Entity<FTLComponent> ent)
    {
        if (!TryComp<SunriseArrivalsShuttleComponent>(ent, out var arrivals))
            return;

        foreach (var dock in arrivals.ReservedDocks)
        {
            RemCompDeferred<FtlReservationComponent>(dock);
        }

        arrivals.ReservedDocks.Clear();
    }

    private void ReserveSunriseFtlDocks(EntityUid shuttleUid, DockingConfig config)
    {
        if (!TryComp<SunriseArrivalsShuttleComponent>(shuttleUid, out var arrivals))
            return;

        foreach (var docks in config.Docks)
        {
            var reservation = EnsureComp<FtlReservationComponent>(docks.DockBUid);
            reservation.ReservedBy = shuttleUid;
            arrivals.ReservedDocks.Add(docks.DockBUid);
        }
    }

    /// <summary>
    /// Возвращает карту FTL для систем Sunrise, не расширяя публичность vanilla-метода.
    /// </summary>
    public EntityUid EnsureSunriseFtlMap()
    {
        return EnsureFTLMap();
    }

    /// <summary>
    /// Запускает FTL к целевой сетке с параметрами Sunrise.
    /// </summary>
    public void FTLToDockSunrise(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityUid target,
        float? startupTime = null,
        float? hyperspaceTime = null,
        string? priorityTag = null,
        bool ignored = false,
        bool deleteObstacles = false)
    {
        if (!TrySetupFTL(shuttleUid, component, out var hyperspace))
            return;

        startupTime ??= DefaultStartupTime;
        hyperspaceTime ??= DefaultTravelTime;

        var config = _dockSystem.GetDockingConfig(shuttleUid, target, priorityTag, ignored);
        hyperspace.StartupTime = startupTime.Value;
        hyperspace.TravelTime = hyperspaceTime.Value;
        hyperspace.StateTime = StartEndTime.FromStartDuration(
            _gameTiming.CurTime,
            TimeSpan.FromSeconds(hyperspace.StartupTime));
        hyperspace.PriorityTag = priorityTag;
        hyperspace.Ignored = ignored;
        hyperspace.DeleteTrash = deleteObstacles;

        _console.RefreshShuttleConsoles(shuttleUid);

        if (config != null)
        {
            hyperspace.TargetCoordinates = config.Coordinates;
            hyperspace.TargetAngle = config.Angle;
            ReserveSunriseFtlDocks(shuttleUid, config);
        }
        else if (TryGetFTLProximity(
                     shuttleUid,
                     new EntityCoordinates(target, Vector2.Zero),
                     out var coordinates,
                     out var angle))
        {
            hyperspace.TargetCoordinates = coordinates;
            hyperspace.TargetAngle = angle;
        }
        else
        {
            hyperspace.TargetCoordinates = Transform(shuttleUid).Coordinates;
            Log.Error($"Unable to FTL grid {ToPrettyString(shuttleUid)} to target properly?");
        }
    }

    /// <summary>
    /// Запускает FTL к заранее выбранной конфигурации стыковки.
    /// </summary>
    public void FTLToDockConfig(
        EntityUid shuttleUid,
        ShuttleComponent component,
        DockingConfig config,
        float? startupTime = null,
        float? hyperspaceTime = null,
        string? priorityTag = null,
        bool ignored = false,
        bool deleteObstacles = false)
    {
        if (!TrySetupFTL(shuttleUid, component, out var hyperspace))
            return;

        startupTime ??= DefaultStartupTime;
        hyperspaceTime ??= DefaultTravelTime;

        hyperspace.StartupTime = startupTime.Value;
        hyperspace.TravelTime = hyperspaceTime.Value;
        hyperspace.StateTime = StartEndTime.FromStartDuration(
            _gameTiming.CurTime,
            TimeSpan.FromSeconds(hyperspace.StartupTime));
        hyperspace.PriorityTag = priorityTag;
        hyperspace.Ignored = ignored;
        hyperspace.DeleteTrash = deleteObstacles;

        _console.RefreshShuttleConsoles(shuttleUid);

        hyperspace.TargetCoordinates = config.Coordinates;
        hyperspace.TargetAngle = config.Angle;
    }

    /// <summary>
    /// Пытается стыковать шаттл с учётом параметров Sunrise.
    /// </summary>
    public bool TrySunriseFtlDock(
        EntityUid shuttleUid,
        ShuttleComponent component,
        EntityUid targetUid,
        [NotNullWhen(true)] out DockingConfig? config,
        string? priorityTag = null,
        bool ignored = false,
        bool deleteObstacles = false)
    {
        config = null;

        if (!_xformQuery.TryGetComponent(shuttleUid, out var shuttleXform) ||
            !_xformQuery.TryGetComponent(targetUid, out var targetXform) ||
            targetXform.MapUid == null ||
            !targetXform.MapUid.Value.IsValid())
        {
            return false;
        }

        config = _dockSystem.GetDockingConfig(shuttleUid, targetUid, priorityTag, ignored);

        if (config != null)
        {
            DockSunriseFtl((shuttleUid, shuttleXform), config, deleteObstacles);
            return true;
        }

        TryFTLProximity(shuttleUid, targetUid, shuttleXform, targetXform);
        return false;
    }

    /// <summary>
    /// Ищет безопасную точку FTL для систем Sunrise, не расширяя публичность vanilla-метода.
    /// </summary>
    public bool TryGetSunriseFtlProximity(
        EntityUid shuttleUid,
        EntityCoordinates targetCoordinates,
        out EntityCoordinates coordinates,
        out Angle angle,
        float minOffset = 0f,
        float maxOffset = 64f,
        TransformComponent? xform = null,
        TransformComponent? targetXform = null)
    {
        return TryGetFTLProximity(
            shuttleUid,
            targetCoordinates,
            out coordinates,
            out angle,
            minOffset,
            maxOffset,
            xform,
            targetXform);
    }

    private DockingConfig? GetSunriseFtlDockingConfigAt(
        EntityUid shuttleUid,
        EntityCoordinates target,
        FTLComponent component)
    {
        return _dockSystem.GetDockingConfigAt(
            shuttleUid,
            target.EntityId,
            target,
            component.TargetAngle,
            ignored: component.Ignored,
            priorityTag: component.PriorityTag);
    }

    private void DockSunriseFtl(
        Entity<TransformComponent> shuttle,
        DockingConfig config,
        bool deleteObstacles)
    {
        FTLDock(shuttle, config);
        DeleteSunriseDockingObstacles(shuttle, config, deleteObstacles);
    }

    private float GetSunriseFtlOffset(EntityUid shuttleUid)
    {
        return HasComp<SunriseArrivalsShuttleComponent>(shuttleUid)
            ? SunriseArrivalsFtlOffset
            : 0f;
    }

    private void SetSunriseFtlVelocity(EntityUid shuttleUid, PhysicsComponent body)
    {
        var speed = _cfg.GetCVar(SunriseCCVars.FTLSpeed);
        _physics.SetLinearVelocity(shuttleUid, new Vector2(0f, speed), body: body);
    }

    /// <summary>
    /// Бросает незакреплённые динамические сущности вдоль движения FTL.
    /// </summary>
    private void DoSunriseFtlThrow(TransformComponent xform, Vector2 throwDirection)
    {
        if (xform.GridUid is not { } gridUid ||
            !TryComp<PhysicsComponent>(gridUid, out var shuttleBody))
        {
            return;
        }

        var toThrow = new ValueList<EntityUid>();
        CollectSunriseFtlThrowTargets(xform, ref toThrow);
        TryComp<MapGridComponent>(gridUid, out var grid);

        foreach (var child in toThrow)
        {
            _stuns.TryUpdateParalyzeDuration(child, _hyperspaceKnockdownTime);

            if (_physicsQuery.TryGetComponent(child, out var physics))
            {
                _throwing.TryThrow(
                    child,
                    throwDirection * SunriseFtlThrowForce,
                    physics,
                    Transform(child),
                    SunriseFtlThrowForce,
                    playSound: false);
            }

            if (grid != null)
                TossIfSpaced((gridUid, grid, shuttleBody), child);
        }
    }

    private void CollectSunriseFtlThrowTargets(TransformComponent xform, ref ValueList<EntityUid> targets)
    {
        var childEnumerator = xform.ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            if (!_physicsQuery.TryGetComponent(child, out var physics) ||
                physics.BodyType is not (BodyType.Dynamic or BodyType.KinematicController))
            {
                continue;
            }

            if (_buckleQuery.TryGetComponent(child, out var buckle) && buckle.Buckled)
                continue;

            if (_movedByPressureQuery.TryComp(child, out var moved) && !moved.Enabled)
                continue;

            targets.Add(child);
        }
    }

    private void DeleteSunriseDockingObstacles(
        Entity<TransformComponent> shuttle,
        DockingConfig config,
        bool deleteObstacles)
    {
        if (!deleteObstacles ||
            !TryComp<FixturesComponent>(shuttle, out var fixtures) ||
            !TryComp<MapGridComponent>(shuttle, out var shuttleGrid))
        {
            return;
        }

        var transform = _physics.GetPhysicsTransform(shuttle.Owner, shuttle.Comp);
        var grids = new List<Entity<MapGridComponent>>();

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            var aabb = fixture.Shape.ComputeAABB(transform, 0)
                .Translated(-shuttleGrid.TileSizeHalfVector);
            grids.Clear();
            _mapManager.FindGridsIntersecting(shuttle.Comp.MapID, aabb, ref grids, includeMap: false);

            foreach (var grid in grids)
            {
                if (grid.Owner == config.TargetGrid || grid.Owner == shuttle.Owner)
                    continue;

                QueueDel(grid);
            }
        }
    }
}
