namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Настройки вампирского action.
/// </summary>
[RegisterComponent]
public sealed partial class VampireActionComponent : Component
{
    /// <summary>
    /// Требуемый уровень.
    /// </summary>
    [DataField]
    public VampirePowerLevel RequiredPowerLevel = VampirePowerLevel.Neonate;

    /// <summary>
    /// Стоимость в крови.
    /// </summary>
    [DataField]
    public int BloodCost;
}
