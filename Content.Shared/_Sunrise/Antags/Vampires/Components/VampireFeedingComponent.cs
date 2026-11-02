using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Состояние питания вампира.
/// </summary>
// Полный снимок обходит ограничение генератора дельт состояния для Dictionary с ключами EntityUid.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class VampireFeedingComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>
    /// Всего крови гуманоидов.
    /// </summary>
    [AutoNetworkedField]
    public int TotalBlood;

    /// <summary>
    /// Предел сытости.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxBloodFullness = 200f;

    /// <summary>
    /// Убывание сытости в секунду.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FullnessDecayPerSecond = 0.15f;

    /// <summary>
    /// Расход крови при голоде.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int StarvationDrunkBloodDrainPerSecond = 2;

    /// <summary>
    /// Остаток расхода крови.
    /// </summary>
    [AutoNetworkedField]
    public float StarvationDrunkBloodDrainAccumulator;

    /// <summary>
    /// Следующее обновление.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate;

    /// <summary>
    /// Предыдущее обновление.
    /// </summary>
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan LastUpdate;

    /// <summary>
    /// Интервал обновления.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Звук укуса.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier BiteSound = new SoundPathSpecifier("/Audio/Effects/bite.ogg");

    /// <summary>
    /// Эффект укуса.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId BiteEffect = "WeaponArcBite";

    /// <summary>
    /// Слоты, способные закрыть рот.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string[] MouthCoveringSlots = ["mask", "head"];

    /// <summary>
    /// Громкость укуса.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BiteVolume = -7f;

    /// <summary>
    /// Укусов до травмы глаз.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BitesPerEyeDamage = 3;

    /// <summary>
    /// Травма глаз от укусов.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int EyeDamage = 1;

    /// <summary>
    /// Интервал глотков.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan SipInterval = TimeSpan.FromSeconds(1.25);

    /// <summary>
    /// Кровь за глоток.
    /// </summary>
    [AutoNetworkedField]
    public float BloodGainPerSip = 10f;

    /// <summary>
    /// Потеря крови цели.
    /// </summary>
    [AutoNetworkedField]
    public float TargetBloodDrainPerSip = 20f;

    /// <summary>
    /// Урон укуса.
    /// </summary>
    [AutoNetworkedField]
    public DamageSpecifier BiteDamage = new();

    /// <summary>
    /// Кровотечение от укуса.
    /// </summary>
    [AutoNetworkedField]
    public float BiteBleedAmount = 1f;

    /// <summary>
    /// Дистанция кормления.
    /// </summary>
    [AutoNetworkedField]
    public float BiteDistanceThreshold = 1.5f;

    /// <summary>
    /// Предел крови с цели.
    /// </summary>
    [AutoNetworkedField]
    public float MaxBloodPerTarget = 200f;

    /// <summary>
    /// Лечение за глоток.
    /// </summary>
    [AutoNetworkedField]
    public DamageSpecifier Healing = new();

    /// <summary>
    /// Активно ли кормление.
    /// </summary>
    [AutoNetworkedField]
    public bool IsDrinking;

    /// <summary>
    /// Кровь с каждой цели.
    /// </summary>
    [AutoNetworkedField]
    public Dictionary<EntityUid, float> BloodDrunkFromTargets = [];

    /// <summary>
    /// Укусы каждой цели.
    /// </summary>
    [AutoNetworkedField]
    public Dictionary<EntityUid, int> BiteCountsByTarget = [];

    /// <summary>
    /// Остаток запаса крови.
    /// </summary>
    [AutoNetworkedField]
    public float DrunkBloodRemainder;

    /// <summary>
    /// Остаток крови прогрессии.
    /// </summary>
    [AutoNetworkedField]
    public float TotalBloodRemainder;

    /// <summary>
    /// Уникальные жертвы.
    /// </summary>
    [AutoNetworkedField]
    public int UniqueVictims;
}
