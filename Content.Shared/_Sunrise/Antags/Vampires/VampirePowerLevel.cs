using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires;

/// <summary>
/// Уровни силы вампира.
/// </summary>
public enum VampirePowerLevel : byte
{
    Neonate,
    Awakened,
    Nightborn,
    Ancient,
    Ascendant,
    Absolute,
}

/// <summary>
/// Настройки уровня силы.
/// </summary>
[Prototype]
public sealed partial class VampirePowerLevelPrototype : IPrototype
{
    /// <inheritdoc />
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Уровень силы.
    /// </summary>
    [DataField(required: true)]
    public VampirePowerLevel Level;

    /// <summary>
    /// Кровь для перехода.
    /// </summary>
    [DataField]
    public int? RequiredTotalBlood;

    /// <summary>
    /// Предел сытости.
    /// </summary>
    [DataField(required: true)]
    public float MaxBloodFullness;

    /// <summary>
    /// Убывание сытости в секунду.
    /// </summary>
    [DataField(required: true)]
    public float FullnessDecayPerSecond;

    /// <summary>
    /// Настройки питания.
    /// </summary>
    [DataField]
    public VampireFangsLevelSettings Fangs = new();

    /// <summary>
    /// Настройки взгляда.
    /// </summary>
    [DataField]
    public VampireGlareLevelSettings Glare = new();

    /// <summary>
    /// Настройки сна.
    /// </summary>
    [DataField]
    public VampireSleepLevelSettings Sleep = new();

    /// <summary>
    /// Настройки омоложения.
    /// </summary>
    [DataField]
    public VampireRejuvenationLevelSettings Rejuvenation = new();
}

/// <summary>
/// Настройки зарядов action.
/// </summary>
[DataDefinition]
public sealed partial class VampireActionChargeSettings
{
    /// <summary>
    /// Предел зарядов.
    /// </summary>
    [DataField]
    public int MaxCharges = 1;

    /// <summary>
    /// Восстановление заряда.
    /// </summary>
    [DataField]
    public TimeSpan RechargeDuration = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Задержка action.
    /// </summary>
    [DataField]
    public TimeSpan UseDelay = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Настройки питания.
/// </summary>
[DataDefinition]
public sealed partial class VampireFangsLevelSettings
{
    /// <summary>
    /// Интервал глотков.
    /// </summary>
    [DataField]
    public TimeSpan SipInterval = TimeSpan.FromSeconds(1.25);

    /// <summary>
    /// Кровь за глоток.
    /// </summary>
    [DataField]
    public float BloodGain = 10f;

    /// <summary>
    /// Потеря крови цели.
    /// </summary>
    [DataField]
    public float TargetBloodDrain = 20f;

    /// <summary>
    /// Урон укуса.
    /// </summary>
    [DataField]
    public DamageSpecifier BiteDamage = new();

    /// <summary>
    /// Кровотечение от укуса.
    /// </summary>
    [DataField]
    public float BleedAmount = 1f;

    /// <summary>
    /// Дистанция кормления.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Предел крови с цели.
    /// </summary>
    [DataField]
    public float MaxBloodPerTarget = 200f;

    /// <summary>
    /// Лечение за глоток.
    /// </summary>
    [DataField]
    public DamageSpecifier Healing = new();
}

/// <summary>
/// Настройки взгляда.
/// </summary>
[DataDefinition]
public sealed partial class VampireGlareLevelSettings
{
    /// <summary>
    /// Настройки action.
    /// </summary>
    [DataField]
    public VampireActionChargeSettings Action = new();

    /// <summary>
    /// Дальность взгляда.
    /// </summary>
    [DataField]
    public float Range = 1f;

    /// <summary>
    /// Паралич спереди.
    /// </summary>
    [DataField]
    public TimeSpan FrontParalyzeDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Паралич сбоку.
    /// </summary>
    [DataField]
    public TimeSpan SideParalyzeDuration = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Урон выносливости.
    /// </summary>
    [DataField]
    public float StaminaDamage = 20f;

    /// <summary>
    /// Доза немого токсина.
    /// </summary>
    [DataField]
    public float MuteToxinAmount = 0.25f;

    /// <summary>
    /// Множитель защиты от вспышек.
    /// </summary>
    [DataField]
    public float FlashProtectionEffectScale;
}

/// <summary>
/// Настройки гипноза.
/// </summary>
[DataDefinition]
public sealed partial class VampireSleepLevelSettings
{
    /// <summary>
    /// Настройки action.
    /// </summary>
    [DataField]
    public VampireActionChargeSettings Action = new();

    /// <summary>
    /// Время гипноза.
    /// </summary>
    [DataField]
    public TimeSpan ChannelTime = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Дальность начала.
    /// </summary>
    [DataField]
    public float TargetRange = 2f;

    /// <summary>
    /// Дистанция прерывания.
    /// </summary>
    [DataField]
    public float BreakRange = 2.5f;

    /// <summary>
    /// Длительность сна.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Стоимость в крови.
    /// </summary>
    [DataField]
    public int BloodCost = 20;

    /// <summary>
    /// Игнорирование веры.
    /// </summary>
    [DataField]
    public bool IgnoresFaith;
}

/// <summary>
/// Настройки омоложения.
/// </summary>
[DataDefinition]
public sealed partial class VampireRejuvenationLevelSettings
{
    /// <summary>
    /// Настройки action.
    /// </summary>
    [DataField]
    public VampireActionChargeSettings Action = new();

    /// <summary>
    /// Восстановление выносливости.
    /// </summary>
    [DataField]
    public float StaminaRestoreAmount;

    /// <summary>
    /// Объём очищения реагентов.
    /// </summary>
    [DataField]
    public float ReagentPurgeAmount;

    /// <summary>
    /// Число применений лечения.
    /// </summary>
    [DataField]
    public int HealApplications;

    /// <summary>
    /// Интервал лечения.
    /// </summary>
    [DataField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(3.5);

    /// <summary>
    /// Лечение за применение.
    /// </summary>
    [DataField]
    public DamageSpecifier Healing = new();
}
