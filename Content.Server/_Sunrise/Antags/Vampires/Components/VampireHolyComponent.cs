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
    /// Время следующей проверки святых уязвимостей.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// Типы распределения физического урона святой воды.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> BruteDamageTypes = new() { "Blunt", "Slash", "Piercing" };

    /// <summary>
    /// Типы распределения ожогового урона святой воды.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> BurnDamageTypes = new() { "Heat", "Shock", "Cold", "Caustic" };

    /// <summary>
    /// Тип урона от святого места.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> HolyPlaceDamageType = "Heat";

    /// <summary>
    /// Порог UniqueVictims для святых уязвимостей.
    /// </summary>
    [DataField]
    public int RequiredVictims = 1;

    /// <summary>
    /// Максимальная потеря запаса крови за срабатывание.
    /// </summary>
    [DataField]
    public int HolyWaterBloodDrain = 3;

    /// <summary>
    /// Суммарный физический урон при наличии запаса крови.
    /// </summary>
    [DataField]
    public float HolyWaterBruteDamage = 3f;

    /// <summary>
    /// Суммарный ожоговый урон при пустом запасе крови.
    /// </summary>
    [DataField]
    public float HolyWaterBurnDamage = 2f;

    /// <summary>
    /// Урон выносливости при наличии запаса крови.
    /// </summary>
    [DataField]
    public float HolyWaterStaminaDamage = 5f;

    /// <summary>
    /// Урон святого места за срабатывание.
    /// </summary>
    [DataField]
    public float HolyPlaceDamage = 3f;

    /// <summary>
    /// Интервал предупреждений о святом месте.
    /// </summary>
    [DataField]
    public TimeSpan PopupInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Интервал эффектов воды и святого места, учитываемый раздельно.
    /// </summary>
    [DataField]
    public TimeSpan EffectInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Радиус поиска доступных закреплённых святынь.
    /// </summary>
    [DataField]
    public float HolyPlaceRange = 8f;

    /// <summary>
    /// Реагент святой воды; эффект его не расходует.
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
    /// Время следующего предупреждения о святом месте.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextHolyPlacePopup;
}
