using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Питание и история укусов, синхронизируемые владельцу.
/// </summary>
// Полный снимок обходит ограничение генератора дельт состояния для Dictionary с ключами EntityUid.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VampireFeedingComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>
    /// Накопленная кровь для прогрессии и целей, без ограничения по виду жертвы.
    /// </summary>
    [AutoNetworkedField]
    public int TotalBlood;

    /// <summary>
    /// Предел сытости текущего уровня силы.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxBloodFullness = 200f;

    /// <summary>
    /// Убывание сытости в секунду.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FullnessDecayPerSecond = 0.15f;

    /// <summary>
    /// Расход запаса крови в секунду при нулевой сытости.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int StarvationDrunkBloodDrainPerSecond = 2;

    /// <summary>
    /// Интервал проверки голода и святых уязвимостей.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public SoundSpecifier BiteSound = new SoundPathSpecifier("/Audio/Effects/bite.ogg");

    [DataField, AutoNetworkedField]
    public EntProtoId BiteEffect = "WeaponArcBite";

    /// <summary>
    /// Слоты, способные закрыть рот.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string[] MouthCoveringSlots = ["mask", "head"];

    /// <summary>
    /// Громкость звука укуса в децибелах.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BiteVolume = -7f;

    /// <summary>
    /// Число глотков с одной цели до повреждения её глаз.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BitesPerEyeDamage = 3;

    /// <summary>
    /// Повреждение глаз каждые BitesPerEyeDamage глотков.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int EyeDamage = 1;

    /// <summary>
    /// Длительность глотка для текущего уровня силы.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan SipInterval = TimeSpan.FromSeconds(1.25);

    /// <summary>
    /// Базовая прибавка крови за полный глоток.
    /// </summary>
    [AutoNetworkedField]
    public float BloodGainPerSip = 10f;

    /// <summary>
    /// Потеря крови цели за полный глоток.
    /// </summary>
    [AutoNetworkedField]
    public float TargetBloodDrainPerSip = 20f;

    /// <summary>
    /// Урон за глоток без учёта сопротивлений.
    /// </summary>
    [AutoNetworkedField]
    public DamageSpecifier BiteDamage = new();

    /// <summary>
    /// Усиление кровотечения за глоток.
    /// </summary>
    [AutoNetworkedField]
    public float BiteBleedAmount = 1f;

    /// <summary>
    /// Предельная дистанция продолжения глотка.
    /// </summary>
    [AutoNetworkedField]
    public float BiteDistanceThreshold = 1.5f;

    /// <summary>
    /// Лимит полученной крови с одной цели.
    /// </summary>
    [AutoNetworkedField]
    public float MaxBloodPerTarget = 200f;

    /// <summary>
    /// Лечение за глоток, пропорциональное полученной крови.
    /// </summary>
    [AutoNetworkedField]
    public DamageSpecifier Healing = new();

    /// <summary>
    /// ID текущего DoAfter питания.
    /// </summary>
    [AutoNetworkedField]
    public ushort? DrinkDoAfter;

    /// <summary>
    /// Полученная кровь по целям; удалённые источники исключаются.
    /// </summary>
    [AutoNetworkedField]
    public Dictionary<EntityUid, float> BloodDrunkFromTargets = [];

    /// <summary>
    /// Глотки по целям с момента последнего повреждения глаз.
    /// </summary>
    [AutoNetworkedField]
    public Dictionary<EntityUid, int> BiteCountsByTarget = [];

    /// <summary>
    /// Дробный остаток пополнения DrunkBlood.
    /// </summary>
    [AutoNetworkedField]
    public float DrunkBloodRemainder;

    /// <summary>
    /// Дробный остаток пополнения TotalBlood.
    /// </summary>
    [AutoNetworkedField]
    public float TotalBloodRemainder;

    /// <summary>
    /// Число учтённых жертв для святых уязвимостей; убийство не требуется.
    /// </summary>
    [AutoNetworkedField]
    public int UniqueVictims;
}
