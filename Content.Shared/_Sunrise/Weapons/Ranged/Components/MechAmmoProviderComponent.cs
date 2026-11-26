using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Weapons.Ranged.Components;

/// <summary>
/// Создаёт боеприпасы за счёт энергии меха, в который установлено оружие.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MechAmmoProviderComponent : AmmoProviderComponent
{
    /// <summary>
    /// Прототип создаваемого снаряда или hitscan-сущности.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Proto;

    /// <summary>
    /// Количество энергии меха, расходуемое на один выстрел.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FireCost = 100f;

    /// <summary>
    /// Мех, в который установлено оружие.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? Mech;
}
