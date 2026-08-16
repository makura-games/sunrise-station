namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Настройки стоимости и минимального уровня силы вампирской способности.
/// </summary>
[RegisterComponent]
public sealed partial class VampireActionComponent : Component
{
    /// <summary>
    /// Минимальный уровень силы для использования способности.
    /// </summary>
    [DataField]
    public VampirePowerLevel RequiredPowerLevel = VampirePowerLevel.Neonate;

    /// <summary>
    /// Стоимость использования способности в накопленной крови.
    /// </summary>
    [DataField]
    public float BloodCost = 0f;
}
