using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Состояние питания вампира.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class VampireFeedingComponent : Component
{
    /// <summary>
    /// Всего крови гуманоидов.
    /// </summary>
    public int TotalBlood;

    /// <summary>
    /// Предел сытости.
    /// </summary>
    [DataField]
    public float MaxBloodFullness = 200f;

    /// <summary>
    /// Убывание сытости в секунду.
    /// </summary>
    [DataField]
    public float FullnessDecayPerSecond = 0.15f;

    /// <summary>
    /// Расход крови при голоде.
    /// </summary>
    [DataField]
    public int StarvationDrunkBloodDrainPerSecond = 2;

    /// <summary>
    /// Остаток расхода крови.
    /// </summary>
    public float StarvationDrunkBloodDrainAccumulator;

    /// <summary>
    /// Следующее обновление.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// Предыдущее обновление.
    /// </summary>
    [AutoPausedField]
    public TimeSpan LastUpdate;

    /// <summary>
    /// Интервал обновления.
    /// </summary>
    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Звук укуса.
    /// </summary>
    [DataField]
    public SoundSpecifier BiteSound = new SoundPathSpecifier("/Audio/Effects/bite.ogg");

    /// <summary>
    /// Эффект укуса.
    /// </summary>
    [DataField]
    public EntProtoId BiteEffect = "WeaponArcBite";

    /// <summary>
    /// Слоты, способные закрыть рот.
    /// </summary>
    [DataField]
    public string[] MouthCoveringSlots = ["mask", "head"];

    /// <summary>
    /// Громкость укуса.
    /// </summary>
    [DataField]
    public float BiteVolume = -7f;

    /// <summary>
    /// Укусов до травмы глаз.
    /// </summary>
    [DataField]
    public int BitesPerEyeDamage = 3;

    /// <summary>
    /// Травма глаз от укусов.
    /// </summary>
    [DataField]
    public int EyeDamage = 1;

    /// <summary>
    /// Интервал глотков.
    /// </summary>
    public TimeSpan SipInterval = TimeSpan.FromSeconds(1.25);

    /// <summary>
    /// Кровь за глоток.
    /// </summary>
    public float BloodGainPerSip = 10f;

    /// <summary>
    /// Потеря крови цели.
    /// </summary>
    public float TargetBloodDrainPerSip = 20f;

    /// <summary>
    /// Урон укуса.
    /// </summary>
    public DamageSpecifier BiteDamage = new();

    /// <summary>
    /// Кровотечение от укуса.
    /// </summary>
    public float BiteBleedAmount = 1f;

    /// <summary>
    /// Дистанция кормления.
    /// </summary>
    public float BiteDistanceThreshold = 1.5f;

    /// <summary>
    /// Предел крови с цели.
    /// </summary>
    public float MaxBloodPerTarget = 200f;

    /// <summary>
    /// Лечение за глоток.
    /// </summary>
    public DamageSpecifier Healing = new();

    /// <summary>
    /// Активно ли кормление.
    /// </summary>
    public bool IsDrinking;

    /// <summary>
    /// Кровь с каждой цели.
    /// </summary>
    public Dictionary<EntityUid, float> BloodDrunkFromTargets = [];

    /// <summary>
    /// Укусы каждой цели.
    /// </summary>
    public Dictionary<EntityUid, int> BiteCountsByTarget = [];

    /// <summary>
    /// Остаток запаса крови.
    /// </summary>
    public float DrunkBloodRemainder;

    /// <summary>
    /// Остаток крови прогрессии.
    /// </summary>
    public float TotalBloodRemainder;

    /// <summary>
    /// Уникальные жертвы.
    /// </summary>
    public int UniqueVictims;
}
