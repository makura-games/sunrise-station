using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Святые уязвимости вампира.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class VampireHolyComponent : Component
{
    /// <summary>
    /// Типы физического урона.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> BruteDamageTypes = ["Blunt", "Slash", "Piercing"];

    /// <summary>
    /// Типы ожогового урона.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> BurnDamageTypes = ["Heat", "Shock", "Cold", "Caustic"];

    /// <summary>
    /// Урон святого места.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> HolyPlaceDamageType = "Heat";

    /// <summary>
    /// Требуемое число жертв.
    /// </summary>
    [DataField]
    public int RequiredVictims = 1;

    /// <summary>
    /// Потеря крови от святой воды.
    /// </summary>
    [DataField]
    public int HolyWaterBloodDrain = 3;

    /// <summary>
    /// Физический урон святой воды.
    /// </summary>
    [DataField]
    public float HolyWaterBruteDamage = 3f;

    /// <summary>
    /// Ожоговый урон святой воды.
    /// </summary>
    [DataField]
    public float HolyWaterBurnDamage = 2f;

    /// <summary>
    /// Урон выносливости святой воды.
    /// </summary>
    [DataField]
    public float HolyWaterStaminaDamage = 5f;

    /// <summary>
    /// Урон святого места.
    /// </summary>
    [DataField]
    public float HolyPlaceDamage = 3f;

    /// <summary>
    /// Интервал предупреждений.
    /// </summary>
    [DataField]
    public TimeSpan PopupInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Интервал святого урона.
    /// </summary>
    [DataField]
    public TimeSpan EffectInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Радиус святого места.
    /// </summary>
    [DataField]
    public float HolyPlaceRange = 8f;

    /// <summary>
    /// Святая вода.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> HolyWaterReagent = "Holywater";

    /// <summary>
    /// Следующий эффект святой воды.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextHolyWaterEffect;

    /// <summary>
    /// Следующий эффект святого места.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextHolyPlaceEffect;

    /// <summary>
    /// Следующее предупреждение.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextHolyPlacePopup;
}
