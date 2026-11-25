using Robust.Shared.GameStates;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Holosign;

public sealed partial class HolosignProjectorComponent
{
    /// <summary>
    /// Максимальное число одинаковых голограмм, размещаемых проектором на одном тайле.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CountPerTileLimit = 3;
}
