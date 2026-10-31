using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    private bool ProcessChargedElectrovaeTiles(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent)
    {
        var atmosphere = ent.Comp1;
        if (!atmosphere.ProcessingPaused)
            QueueRunTiles(atmosphere.CurrentRunTiles, atmosphere.ChargedElectrovaeTiles);

        var number = 0;
        while (atmosphere.CurrentRunTiles.TryDequeue(out var tile))
        {
            ProcessChargedElectrovae(ent, tile);

            if (number++ < LagCheckIterations)
                continue;

            number = 0;
            if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
            {
                return false;
            }
        }

        CleanupChargedElectrovaeEntities((ent.Owner, atmosphere));
        return true;
    }
}
