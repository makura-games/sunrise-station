using Robust.Shared.Audio;
using Content.Server.Power.Components;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server._Sunrise.EnergyShield;

[RegisterComponent]
[Access(typeof(EnergyShieldSystem))]
public sealed partial class EnergyShieldComponent : Component
{
    /// <summary>
    /// Стоимость энергии за единицу урона
    /// </summary>
    [DataField]
    public float EnergyCostPerDamage = 15f;

    /// <summary>
    /// Звук поглощения урона
    /// </summary>
    [DataField]
    public SoundSpecifier AbsorbSound = new SoundPathSpecifier("/Audio/_Sunrise/Machines/energyshield_parry.ogg");

    /// <summary>
    /// Звук отключения щита при нехватке энергии
    /// </summary>
    [DataField]
    public SoundSpecifier ShutdownSound = new SoundPathSpecifier("/Audio/_Sunrise/Machines/energyshield_down.ogg");

    /// <summary>
    /// При скольки процентах заряда можно включить щит
    /// </summary>
    [DataField]
    public float MinChargeFractionForActivation = 0.05f;
}
