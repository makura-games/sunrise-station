using Content.Shared.Atmos;

namespace Content.Shared.Atmos.Components;

public sealed partial class GridAtmosphereComponent
{
    [ViewVariables]
    public readonly HashSet<TileAtmosphere> ChargedElectrovaeTiles = new(1000);

    [ViewVariables]
    public int ChargedElectrovaeTilesCount => ChargedElectrovaeTiles.Count;
}
