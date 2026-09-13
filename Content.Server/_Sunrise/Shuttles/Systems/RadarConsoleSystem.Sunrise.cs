using Content.Server._Starlight.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Shuttles.Systems;

public sealed partial class RadarConsoleSystem
{
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private RadarLaserSystem _laserSystem = default!;
    [Dependency] private IGameTiming _timing = default!;

    // How often (in seconds) to push fresh blip state to all open radar consoles.
    private const float BlipUpdateInterval = 0.1f;
    private float _blipUpdateTimer = 0f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _blipUpdateTimer += frameTime;
        if (_blipUpdateTimer < BlipUpdateInterval)
            return;
        _blipUpdateTimer = 0f;

        // Prune expired Apollo laser traces before syncing state.
        _laserSystem.PruneExpiredTraces((float) _timing.CurTime.TotalSeconds);

        var query = AllEntityQuery<RadarConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateState(uid, comp);
        }
    }

    private void PopulateSunriseRadarState(EntityUid uid, NavInterfaceState state)
    {
        // Populate radar blips for entities with RadarBlipComponent (e.g. artillery shells).
        var consoleMapCoords = _transformSystem.GetMapCoordinates(uid);
        var maxRangeSq = state.MaxRange * state.MaxRange;
        var blipQuery = AllEntityQuery<RadarBlipComponent, TransformComponent>();
        while (blipQuery.MoveNext(out var blipUid, out var blip, out var blipXform))
        {
            if (blip.RequireInSpace && blipXform.GridUid != null)
                continue;
            if (blipXform.MapID != consoleMapCoords.MapId)
                continue;

            var blipMapCoords = _transformSystem.GetMapCoordinates(blipUid, blipXform);
            if ((blipMapCoords.Position - consoleMapCoords.Position).LengthSquared() > maxRangeSq)
                continue;

            state.Blips.Add(new RadarBlipData(GetNetCoordinates(blipXform.Coordinates), blip.Color, blip.Scale, blip.Shape));
        }

        // Populate laser traces from hitscan guns with RadarLaserTrackerComponent.
        var laserQuery = AllEntityQuery<RadarLaserTrackerComponent, TransformComponent>();
        while (laserQuery.MoveNext(out var laserUid, out var tracker, out var laserXform))
        {
            if (laserXform.MapID != consoleMapCoords.MapId)
                continue;

            foreach (var (origin, dir, _) in tracker.Traces)
            {
                // Only show traces from guns within radar range.
                if ((origin.Position - consoleMapCoords.Position).LengthSquared() > maxRangeSq)
                    continue;

                state.Lasers.Add(new RadarLaserData(
                    GetNetCoordinates(laserXform.Coordinates),
                    dir,
                    tracker.MaxRange,
                    tracker.LaserColor));
            }
        }
    }
}
