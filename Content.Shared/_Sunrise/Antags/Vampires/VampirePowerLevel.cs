using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires;

public enum VampirePowerLevel : byte
{
    Neonate,
    Awakened,
    Nightborn,
    Ancient,
    Ascendant,
    Absolute,
}

[Prototype]
public sealed partial class VampirePowerLevelPrototype : IPrototype
{
    /// <inheritdoc />
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public VampirePowerLevel Level;

    /// <summary>
    /// Порог TotalBlood; null отключает автоматическое повышение до этого уровня.
    /// </summary>
    [DataField]
    public int? RequiredTotalBlood;

    [DataField(required: true)]
    public float MaxBloodFullness;

    [DataField(required: true)]
    public float FullnessDecayPerSecond;

    [DataField]
    public VampireFangsLevelSettings Fangs = new();

    [DataField]
    public VampireGlareLevelSettings Glare = new();

    [DataField]
    public VampireSleepLevelSettings Sleep = new();

    [DataField]
    public VampireRejuvenationLevelSettings Rejuvenation = new();
}

[DataDefinition]
public sealed partial class VampireActionChargeSettings
{
    [DataField]
    public int MaxCharges = 1;

    /// <summary>
    /// Время восстановления одного заряда.
    /// </summary>
    [DataField]
    public TimeSpan RechargeDuration = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Кулдаун применения, отдельный от восстановления зарядов.
    /// </summary>
    [DataField]
    public TimeSpan UseDelay = TimeSpan.FromSeconds(10);
}

[DataDefinition]
public sealed partial class VampireFangsLevelSettings
{
    [DataField]
    public TimeSpan SipInterval = TimeSpan.FromSeconds(1.25);

    /// <summary>
    /// Базовая прибавка крови за полный глоток.
    /// </summary>
    [DataField]
    public float BloodGain = 10f;

    /// <summary>
    /// Потеря крови цели за полный глоток.
    /// </summary>
    [DataField]
    public float TargetBloodDrain = 20f;

    [DataField]
    public DamageSpecifier BiteDamage = new();

    /// <summary>
    /// Усиление кровотечения за глоток.
    /// </summary>
    [DataField]
    public float BleedAmount = 1f;

    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Лимит полученной крови с одной цели.
    /// </summary>
    [DataField]
    public float MaxBloodPerTarget = 200f;

    [DataField]
    public DamageSpecifier Healing = new();
}

[DataDefinition]
public sealed partial class VampireGlareLevelSettings
{
    [DataField]
    public VampireActionChargeSettings Action = new();

    [DataField]
    public float Range = 1f;

    /// <summary>
    /// Паралич стоящей цели спереди.
    /// </summary>
    [DataField]
    public TimeSpan FrontParalyzeDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Паралич сбоку или для сбитой с ног цели.
    /// </summary>
    [DataField]
    public TimeSpan SideParalyzeDuration = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Урон выносливости независимо от направления.
    /// </summary>
    [DataField]
    public float StaminaDamage = 20f;

    /// <summary>
    /// Доза токсина немоты стоящей цели спереди.
    /// </summary>
    [DataField]
    public float MuteToxinAmount = 0.25f;

    /// <summary>
    /// Множитель эффектов при защите от вспышек; ноль блокирует их.
    /// </summary>
    [DataField]
    public float FlashProtectionEffectScale;
}

[DataDefinition]
public sealed partial class VampireSleepLevelSettings
{
    [DataField]
    public VampireActionChargeSettings Action = new();

    [DataField]
    public TimeSpan ChannelTime = TimeSpan.FromSeconds(6);

    [DataField]
    public float TargetRange = 2f;

    [DataField]
    public float BreakRange = 2.5f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    [DataField]
    public int BloodCost = 20;

    /// <summary>
    /// Обход только защиты веры.
    /// </summary>
    [DataField]
    public bool IgnoresFaith;
}

[DataDefinition]
public sealed partial class VampireRejuvenationLevelSettings
{
    [DataField]
    public VampireActionChargeSettings Action = new();

    [DataField]
    public float StaminaRestoreAmount;

    /// <summary>
    /// Общий объём удаляемых реагентов при улучшенном омоложении.
    /// </summary>
    [DataField]
    public float ReagentPurgeAmount;

    /// <summary>
    /// Число шагов лечения улучшенного омоложения.
    /// </summary>
    [DataField]
    public int HealApplications;

    [DataField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(3.5);

    [DataField]
    public DamageSpecifier Healing = new();
}
