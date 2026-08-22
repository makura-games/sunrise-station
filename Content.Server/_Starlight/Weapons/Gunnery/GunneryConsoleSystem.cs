using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.UserInterface;
using Content.Shared._Starlight.Weapons.Gunnery;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Weapons.Gunnery;

/// <summary>
/// Server-side logic for the gunnery console.
/// Periodically broadcasts an updated <see cref="GunneryConsoleBoundUserInterfaceState"/>
/// containing the standard radar data, cannon blip positions, and guided-projectile
/// tracking info. Also handles fire and guidance BUI messages from the client.
/// </summary>
public sealed partial class GunneryConsoleSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem   _ui        = default!;
    [Dependency] private ShuttleConsoleSystem  _console   = default!;
    [Dependency] private SharedGunSystem       _gun       = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming           _timing    = default!;

    private const float UpdateInterval = 0.25f;
    private float _updateTimer;
    private readonly Dictionary<EntityUid, PendingFireContext> _pendingFireContexts = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunneryConsoleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GunComponent, AmmoShotEvent>(OnAmmoShot);

        Subs.BuiEvents<GunneryConsoleComponent>(GunneryConsoleUiKey.Key, subs =>
        {
            subs.Event<GunneryConsoleFireStartMessage>(OnFireStartMessage);
            subs.Event<GunneryConsoleFireStopMessage>(OnFireStopMessage);
            subs.Event<GunneryConsoleGuidanceMessage>(OnGuidanceMessage);
        });
    }

    // ── Update loop ────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Handle held-fire every frame so automatic weapons keep their native cadence.
        var heldFireQuery = AllEntityQuery<GunneryConsoleComponent>();
        while (heldFireQuery.MoveNext(out var consoleUid, out var consoleComp))
            ProcessHeldFire(consoleUid, consoleComp);

        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval)
            return;

        _updateTimer = 0f;

        var query = AllEntityQuery<GunneryConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
            UpdateState(uid, comp);
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnStartup(EntityUid uid, GunneryConsoleComponent comp, ComponentStartup args)
    {
        UpdateState(uid, comp);
    }

    private void OnAmmoShot(EntityUid uid, GunComponent gun, AmmoShotEvent args)
    {
        if (!_pendingFireContexts.TryGetValue(uid, out var pending))
            return;

        if (!TryComp<GunneryConsoleComponent>(pending.Console, out var consoleComp))
            return;

        foreach (var projectileUid in args.FiredProjectiles)
        {
            if (!TryComp<GuidedProjectileComponent>(projectileUid, out var guided))
                continue;

            guided.Controller = pending.Console;
            guided.SteeringTarget = pending.Target;
            guided.Active = true;
            consoleComp.TrackedGuidedProjectile = projectileUid;
        }
    }

    private void OnFireStartMessage(EntityUid uid, GunneryConsoleComponent comp, GunneryConsoleFireStartMessage msg)
    {
        var cannon = GetEntity(msg.Cannon);
        var targetCoords = GetCoordinates(msg.Target);

        if (!TryValidateFireRequest(uid, comp, cannon, targetCoords, out var gunComp))
            return;

        comp.ReleaseRequested = false;
        comp.HeldCannons[cannon] = targetCoords;
        TryFireCannon(uid, comp, cannon, gunComp, targetCoords);
    }

    private void OnFireStopMessage(EntityUid uid, GunneryConsoleComponent comp, GunneryConsoleFireStopMessage msg)
    {
        comp.ReleaseRequested = true;
    }

    private void ProcessHeldFire(EntityUid uid, GunneryConsoleComponent comp)
    {
        if (comp.HeldCannons.Count == 0)
            return;

        var invalid = new List<EntityUid>();
        foreach (var (cannon, targetCoords) in comp.HeldCannons)
        {
            if (!TryValidateFireRequest(uid, comp, cannon, targetCoords, out var gunComp))
            {
                invalid.Add(cannon);
                continue;
            }

            // On release: full-auto and semi stop immediately. Burst is allowed to finish.
            if (comp.ReleaseRequested
                && gunComp.SelectedMode != SelectiveFire.Burst
                && !gunComp.BurstActivated)
            {
                invalid.Add(cannon);
                continue;
            }

            TryFireCannon(uid, comp, cannon, gunComp, targetCoords);

            if (comp.ReleaseRequested && !gunComp.BurstActivated)
                invalid.Add(cannon);
        }

        foreach (var bad in invalid)
            comp.HeldCannons.Remove(bad);

        if (comp.ReleaseRequested && comp.HeldCannons.Count == 0)
            comp.ReleaseRequested = false;
    }

    private void TryFireCannon(EntityUid consoleUid, GunneryConsoleComponent comp, EntityUid cannon, GunComponent gunComp, EntityCoordinates targetCoords)
    {
        if (!_gun.CanShoot(gunComp))
            return;

        // Rotate cannon to face the target before firing so it visually aims correctly.
        var cannonMapPos = _transform.GetMapCoordinates(cannon);
        var targetMapPos = _transform.ToMapCoordinates(targetCoords);
        if (cannonMapPos.MapId == targetMapPos.MapId)
        {
            // Robust entity rotation uses 0=south as the sprite default (CCW positive).
            // ToAngle() uses 0=east (standard math). The offset is +π/2 to convert.
            var aimAngle = (targetMapPos.Position - cannonMapPos.Position).ToAngle() + new Angle(Math.PI / 2);
            _transform.SetWorldRotation(cannon, aimAngle);
        }

        // Record fire time and target before shooting so OnGuidedProjectileStartup can claim
        // the spawned entity and activate tracking toward the clicked position.
        comp.LastFireTime       = _timing.CurTime;
        comp.LastFireTargetPos  = targetMapPos.Position;

        // Pass cannon as the "user" so AttemptShoot uses the cannon's world position as the
        // projectile spawn origin instead of the player's position.
        _pendingFireContexts[cannon] = new PendingFireContext
        {
            Console = consoleUid,
            Target = targetMapPos.Position,
        };

        try
        {
            _gun.AttemptShoot(cannon, cannon, gunComp, targetCoords);
        }
        finally
        {
            _pendingFireContexts.Remove(cannon);
        }
    }

    private void OnGuidanceMessage(EntityUid uid, GunneryConsoleComponent comp, GunneryConsoleGuidanceMessage msg)
    {
        if (!TryGetConsoleMap(uid, out var consoleMapCoords))
            return;

        // If no projectile tracked yet, try to find one controlled by this console.
        if (comp.TrackedGuidedProjectile == null || !Exists(comp.TrackedGuidedProjectile.Value))
        {
            comp.TrackedGuidedProjectile = FindControlledProjectile(uid);
            if (comp.TrackedGuidedProjectile == null)
                return;
        }

        if (!TryComp<GuidedProjectileComponent>(comp.TrackedGuidedProjectile.Value, out var guided))
        {
            comp.TrackedGuidedProjectile = null;
            return;
        }

        // Never trust client-side steering targets: only allow controlling projectiles
        // that this console owns and only toward coordinates on the same map.
        if (guided.Controller != uid || !TryGetConsoleMap(comp.TrackedGuidedProjectile.Value, out var projectileMapCoords))
        {
            comp.TrackedGuidedProjectile = null;
            return;
        }

        var targetCoords = GetCoordinates(msg.Target);
        if (!Exists(targetCoords.EntityId))
            return;

        var targetMapCoords = _transform.ToMapCoordinates(targetCoords);
        if (targetMapCoords.MapId != consoleMapCoords.MapId || targetMapCoords.MapId != projectileMapCoords.MapId)
            return;

        guided.SteeringTarget = targetMapCoords.Position;
        guided.Active         = true;
        guided.Controller     = uid;
    }

    // ── State building ─────────────────────────────────────────────────────

    private void UpdateState(EntityUid uid, GunneryConsoleComponent comp)
    {
        if (!_ui.HasUi(uid, GunneryConsoleUiKey.Key))
            return;

        var xform             = Transform(uid);
        EntityCoordinates? coordinates = null;
        Angle?             angle       = null;

        if (xform.ParentUid == xform.GridUid)
        {
            coordinates = xform.Coordinates;
            angle       = xform.LocalRotation;
        }

        var docks    = _console.GetAllDocks();
        NavInterfaceState navState;

        if (coordinates != null && angle != null)
            navState = _console.GetNavState(uid, docks, coordinates.Value, angle.Value);
        else
            navState = _console.GetNavState(uid, docks);

        navState.MaxRange = comp.MaxRange;

        // Populate standard radar blips (rockets, shells, etc.)
        var consoleMapCoords = _transform.GetMapCoordinates(uid);
        var maxRangeSq       = comp.MaxRange * comp.MaxRange;

        var blipQuery = AllEntityQuery<RadarBlipComponent, TransformComponent>();
        while (blipQuery.MoveNext(out var blipUid, out var blip, out var blipXform))
        {
            if (blip.RequireInSpace && blipXform.GridUid != null)
                continue;
            if (blipXform.MapID != consoleMapCoords.MapId)
                continue;

            var blipMapCoords = _transform.GetMapCoordinates(blipUid, blipXform);
            if ((blipMapCoords.Position - consoleMapCoords.Position).LengthSquared() > maxRangeSq)
                continue;

            navState.Blips.Add(new RadarBlipData(
                GetNetCoordinates(blipXform.Coordinates),
                blip.Color,
                blip.Scale,
                blip.Shape));
        }

        // Populate laser traces from hitscan shuttle guns with RadarLaserTrackerComponent.
        var laserQuery = AllEntityQuery<RadarLaserTrackerComponent, TransformComponent>();
        while (laserQuery.MoveNext(out var laserUid, out var tracker, out var laserXform))
        {
            if (laserXform.MapID != consoleMapCoords.MapId)
                continue;

            foreach (var (origin, dir, _) in tracker.Traces)
            {
                if ((origin.Position - consoleMapCoords.Position).LengthSquared() > maxRangeSq)
                    continue;

                navState.Lasers.Add(new RadarLaserData(
                    GetNetCoordinates(laserXform.Coordinates),
                    dir,
                    tracker.MaxRange,
                    tracker.LaserColor));
            }
        }

        // Build cannon blip list: all GunneryTrackable guns on the same grid.
        var cannons = new List<CannonBlipData>();
        var gridId  = xform.GridUid;

        if (gridId != null && HasComp<MapGridComponent>(gridId.Value))
        {
            var gunQuery = AllEntityQuery<GunneryTrackableComponent, GunComponent, TransformComponent>();
            while (gunQuery.MoveNext(out var gunUid, out _, out var gunComp, out var gunXform))
            {
                if (gunXform.GridUid != gridId)
                    continue;

                var gunMapCoords = _transform.GetMapCoordinates(gunUid, gunXform);
                if (gunMapCoords.MapId != consoleMapCoords.MapId)
                    continue;

                if ((gunMapCoords.Position - consoleMapCoords.Position).LengthSquared() > maxRangeSq)
                    continue;

                var cooldown = (float)Math.Max(0.0, (gunComp.NextFire - _timing.CurTime).TotalSeconds);
                cannons.Add(new CannonBlipData(
                    GetNetCoordinates(gunXform.Coordinates),
                    GetNetEntity(gunUid),
                    MetaData(gunUid).EntityName,
                    cooldown));
            }
        }

        // Clean up tracked projectile if it has been destroyed.
        if (comp.TrackedGuidedProjectile != null && !Exists(comp.TrackedGuidedProjectile.Value))
            comp.TrackedGuidedProjectile = null;

        var trackedNet = comp.TrackedGuidedProjectile.HasValue
            ? GetNetEntity(comp.TrackedGuidedProjectile.Value)
            : (NetEntity?) null;

        _ui.SetUiState(uid, GunneryConsoleUiKey.Key,
            new GunneryConsoleBoundUserInterfaceState(navState, cannons, trackedNet));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Scans for any <see cref="GuidedProjectileComponent"/> whose controller is this console.</summary>
    private EntityUid? FindControlledProjectile(EntityUid consoleUid)
    {
        var query = AllEntityQuery<GuidedProjectileComponent>();
        while (query.MoveNext(out var uid, out var guided))
        {
            if (guided.Controller == consoleUid)
                return uid;
        }

        return null;
    }

    private bool TryValidateFireRequest(
        EntityUid consoleUid,
        GunneryConsoleComponent consoleComp,
        EntityUid cannon,
        EntityCoordinates targetCoords,
        out GunComponent gunComp)
    {
        gunComp = default!;

        if (!Exists(cannon))
            return false;

        if (!TryComp<GunComponent>(cannon, out var gun) || !HasComp<GunneryTrackableComponent>(cannon))
            return false;
        gunComp = gun;

        if (!TryGetConsoleGrid(consoleUid, out var consoleGrid))
            return false;

        if (!TryComp(cannon, out TransformComponent? cannonXform) || cannonXform.GridUid != consoleGrid)
            return false;

        if (!TryGetConsoleMap(consoleUid, out var consoleMapCoords))
            return false;

        var cannonMapCoords = _transform.GetMapCoordinates(cannon, cannonXform);
        if (cannonMapCoords.MapId != consoleMapCoords.MapId)
            return false;

        if (!Exists(targetCoords.EntityId))
            return false;

        var targetMapCoords = _transform.ToMapCoordinates(targetCoords);
        if (targetMapCoords.MapId != consoleMapCoords.MapId)
            return false;

        var maxRangeSq = consoleComp.MaxRange * consoleComp.MaxRange;
        if ((targetMapCoords.Position - consoleMapCoords.Position).LengthSquared() > maxRangeSq)
            return false;

        return true;
    }

    private bool TryGetConsoleGrid(EntityUid consoleUid, out EntityUid gridUid)
    {
        gridUid = default;
        var xform = Transform(consoleUid);
        if (xform.GridUid == null || !HasComp<MapGridComponent>(xform.GridUid.Value))
            return false;

        gridUid = xform.GridUid.Value;
        return true;
    }

    private bool TryGetConsoleMap(EntityUid uid, out MapCoordinates mapCoords)
    {
        mapCoords = _transform.GetMapCoordinates(uid);
        return mapCoords.MapId != MapId.Nullspace;
    }

    private struct PendingFireContext
    {
        public EntityUid Console;
        public Vector2 Target;
    }
}
