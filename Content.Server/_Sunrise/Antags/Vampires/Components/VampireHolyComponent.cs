using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Antags.Vampires.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class VampireHolyComponent : Component
{
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

    [AutoPausedField]
    public TimeSpan NextHolyWaterEffect;

    [AutoPausedField]
    public TimeSpan NextHolyPlaceEffect;

    [AutoPausedField]
    public TimeSpan NextHolyPlacePopup;
}
