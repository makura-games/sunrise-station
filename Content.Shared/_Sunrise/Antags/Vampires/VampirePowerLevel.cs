using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires;

/// <summary>
/// Уровень силы вампира. Порядок значений определяет порядок прогрессии
/// </summary>
public enum VampirePowerLevel : byte
{
    Neonate,    // Начальный уровень силы
    Awakened,
    Nightborn,
    Ancient,    // Максимальный уровень силы, который может быть достигнут через накопленную кровь
    Ascendant,  // КРОВЬ НЕ ДОЛЖНА ОТКРЫВАТЬ ДАННЫЙ УРОВЕНЬ СИЛЫ
                // Сейчас не используется, считается "максимальным уровнем силы", который может быть достигнут через специальные цели
    Absolute,   // КРОВЬ НЕ ДОЛЖНА ОТКРЫВАТЬ ДАННЫЙ УРОВЕНЬ СИЛЫ
                // Сейчас не используется, только для админов, НЕ ИСПОЛЬЗОВАТЬ в качестве уровня силы в геймплее
}

/// <summary>
/// Настройки автоматического достижения уровня силы вампира.
/// Отсутствующий <see cref="RequiredTotalBlood"/> запрещает открывать уровень выпитой кровью.
/// </summary>
[Prototype]
public sealed partial class VampirePowerLevelPrototype : IPrototype
{
    /// <inheritdoc />
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Уровень силы...
    /// </summary>
    [DataField(required: true)]
    public VampirePowerLevel Level;

    /// <summary>
    /// Сколько всего крови нужно выпить для автоматического достижения уровня.
    /// </summary>
    [DataField]
    public int? RequiredTotalBlood;

    /// <summary>
    /// Максимальный запас сытости кровью.
    /// </summary>
    [DataField(required: true)]
    public float MaxBloodFullness;

    /// <summary>
    /// Ежесекундная потеря сытости кровью.
    /// </summary>
    [DataField(required: true)]
    public float FullnessDecayPerSecond;

    /// <summary>
    /// Настройки кормления на этом уровне силы.
    /// </summary>
    [DataField(required: true)]
    public VampireFangsLevelSettings Fangs = new();

    /// <summary>
    /// Настройки вампирского взгляда на этом уровне силы.
    /// </summary>
    [DataField(required: true)]
    public VampireGlareLevelSettings Glare = new();

    /// <summary>
    /// Настройки гипнотического сна на этом уровне силы.
    /// </summary>
    [DataField(required: true)]
    public VampireSleepLevelSettings Sleep = new();

    /// <summary>
    /// Настройки омоложения на этом уровне силы.
    /// </summary>
    [DataField(required: true)]
    public VampireRejuvenationLevelSettings Rejuvenation = new();
}

/// <summary>
/// Общие настройки зарядов и задержки action.
/// </summary>
[DataDefinition]
public sealed partial class VampireActionChargeSettings
{
    [DataField(required: true)]
    public int MaxCharges = 1;

    [DataField(required: true)]
    public TimeSpan RechargeDuration = TimeSpan.FromSeconds(90);

    [DataField(required: true)]
    public TimeSpan UseDelay = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Настройки кормления вампира.
/// </summary>
[DataDefinition]
public sealed partial class VampireFangsLevelSettings
{
    [DataField(required: true)]
    public TimeSpan SipInterval = TimeSpan.FromSeconds(1.25);

    [DataField(required: true)]
    public float BloodGain = 10f;

    [DataField(required: true)]
    public float TargetBloodDrain = 20f;

    [DataField(required: true)]
    public float AnimalEfficiency = 0.05f;

    [DataField(required: true)]
    public float CorpseEfficiency = 0.1f;

    [DataField(required: true)]
    public float PierceDamage = 0.5f;

    [DataField(required: true)]
    public float BleedAmount = 1f;

    [DataField(required: true)]
    public float Range = 1.5f;

    [DataField(required: true)]
    public float MaxBloodPerTarget = 200f;

    [DataField(required: true)]
    public float HealBrute = 1f;

    [DataField(required: true)]
    public float HealBurn = 1f;

    [DataField(required: true)]
    public float HealPoison = 2f;

    [DataField(required: true)]
    public float HealAsphyxiation = 4f;
}

/// <summary>
/// Настройки вампирского взгляда.
/// </summary>
[DataDefinition]
public sealed partial class VampireGlareLevelSettings
{
    [DataField(required: true)]
    public VampireActionChargeSettings Action = new();

    [DataField(required: true)]
    public float Range = 1f;

    [DataField(required: true)]
    public TimeSpan FrontParalyzeDuration = TimeSpan.FromSeconds(3);

    [DataField(required: true)]
    public TimeSpan SideParalyzeDuration = TimeSpan.FromSeconds(1);

    [DataField(required: true)]
    public float StaminaDamage = 20f;

    [DataField(required: true)]
    public float MuteToxinAmount = 0.25f;

    [DataField(required: true)]
    public float FlashProtectionEffectScale;
}

/// <summary>
/// Настройки гипнотического сна.
/// </summary>
[DataDefinition]
public sealed partial class VampireSleepLevelSettings
{
    [DataField(required: true)]
    public VampireActionChargeSettings Action = new();

    [DataField(required: true)]
    public TimeSpan ChannelTime = TimeSpan.FromSeconds(6);

    [DataField(required: true)]
    public float TargetRange = 2f;

    [DataField(required: true)]
    public float BreakRange = 2.5f;

    [DataField(required: true)]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    [DataField(required: true)]
    public int BloodCost = 20;

    [DataField]
    public bool IgnoresFaith;
}

/// <summary>
/// Настройки омоложения.
/// </summary>
[DataDefinition]
public sealed partial class VampireRejuvenationLevelSettings
{
    [DataField(required: true)]
    public VampireActionChargeSettings Action = new();

    [DataField]
    public float ReagentPurgeAmount;

    [DataField]
    public int HealTicks;

    [DataField]
    public TimeSpan HealTickInterval = TimeSpan.FromSeconds(3.5);

    [DataField]
    public float HealBrute;

    [DataField]
    public float HealBurn;

    [DataField]
    public float HealPoison;

    [DataField]
    public float HealAsphyxiation;
}
