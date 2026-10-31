using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Map;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Holosign;

public sealed partial class HolosignSystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private const float SunriseTileSearchRadius = 0.1f;

    private bool CanPlaceSunriseHolosign(
        Entity<HolosignProjectorComponent> ent,
        EntityCoordinates coordinates)
    {
        var tileCoordinates = coordinates.SnapToGrid(EntityManager);
        var mapCoordinates = _transform.ToMapCoordinates(tileCoordinates);
        var count = 0;

        foreach (var uid in _lookup.GetEntitiesInRange(mapCoordinates, SunriseTileSearchRadius))
        {
            if (MetaData(uid).EntityPrototype?.ID != ent.Comp.SignProto.Id)
                continue;

            count++;
            if (count >= ent.Comp.CountPerTileLimit)
                return false;
        }

        return true;
    }
}
