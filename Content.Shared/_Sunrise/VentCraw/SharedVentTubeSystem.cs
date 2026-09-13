using System.Linq;
using Content.Shared._Sunrise.VentCraw.Components;
using Robust.Shared.Map.Components;

namespace Content.Shared._Sunrise.VentCraw;

public sealed partial class SharedVentTubeSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _mapSystem = default!;

    public EntityUid? NextTubeFor(EntityUid target, Direction nextDirection, VentCrawTubeComponent? targetTube = null)
    {
        if (!Resolve(target, ref targetTube))
            return null;
        var oppositeDirection = nextDirection.GetOpposite();

        var xform = Transform(target);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return null;

        if (xform.GridUid == null)
            return null;

        var position = xform.Coordinates;
        foreach (EntityUid entity in _mapSystem.GetInDir(xform.GridUid.Value, grid ,position, nextDirection))
        {

            if (!TryComp(entity, out VentCrawTubeComponent? tube)
                || !CanConnect(target, targetTube, nextDirection)
                || !CanConnect(entity, tube, oppositeDirection))
                continue;

            return entity;
        }

        return null;
    }

    private bool CanConnect(EntityUid tubeId, VentCrawTubeComponent tube, Direction direction)
    {
        if (!tube.Connected)
        {
            return false;
        }

        var ev = new GetVentCrawsConnectableDirectionsEvent();
        RaiseLocalEvent(tubeId, ref ev);
        return ev.Connectable.Contains(direction);
    }
}
