using Robust.Shared.GameStates;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Holosign;

public sealed partial class HolosignProjectorComponent
{
    /// <summary>
    /// Максимальное число одинаковых голограмм, размещаемых проектором на одном тайле.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CountPerTileLimit = 3;
}
