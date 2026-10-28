using Robust.Shared.GameStates;

#pragma warning disable IDE0130 // Namespace не соответствует структуре папок проекта.
namespace Content.Shared.Weapons.Ranged.Components;

public sealed partial class ChamberMagazineAmmoProviderComponent
{
    /// <summary>
    /// Whether the bolt opens after firing the final round.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BoltOpenOnEmpty = true;
}
