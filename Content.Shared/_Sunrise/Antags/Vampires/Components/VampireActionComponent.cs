using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Настройки вампирского действия.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class VampireActionComponent : Component
{
    /// <summary>
    /// Требуемый уровень.
    /// </summary>
    [DataField, AutoNetworkedField]
    public VampirePowerLevel RequiredPowerLevel = VampirePowerLevel.Neonate;

    /// <summary>
    /// Стоимость в крови.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BloodCost;
}
