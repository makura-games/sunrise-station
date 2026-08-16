using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;


namespace Content.Shared._Sunrise.Antags.Vampires.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]

public sealed partial class VampireComponent : Component
{
    /// <summary>
    /// Default abilities, they will be added at start.
    /// </summary>
    [DataField]
    public List<EntProtoId> BaseVampireActions = new()
    {
        "ActionVampireToggleFangs",
        "ActionVampireGlare",
        "ActionVampireRejuvenateI",
        "ActionVampireSleep"
    };

    [DataField]
    public List<EntProtoId> RejuvenateActions = new()
    {
        "ActionVampireRejuvenateI",
        "ActionVampireRejuvenateII"
    };

    /// <summary>
    /// Lifetime total blood drunk. Used for unlocking abilities.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TotalBlood = 0;

    /// <summary>
    /// Total blood drunk by this vampire, used for blood cost calculations.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int DrunkBlood = 0;

    /// <summary>
    /// Determines whether the fangs are extended or not.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool FangsExtended = false;

    /// <summary>
    /// Время между последовательными укусами.
    /// </summary>
    [DataField]
    public TimeSpan SipInterval = TimeSpan.FromSeconds(1.25);

    /// <summary>
    /// Количество крови, получаемое из живого гуманоида за укус.
    /// </summary>
    [DataField]
    public float BloodGainPerSip = 10f;

    /// <summary>
    /// Количество крови, отнимаемое у цели за укус.
    /// </summary>
    [DataField]
    public float TargetBloodDrainPerSip = 20f;

    /// <summary>
    /// Эффективность крови животных относительно живого гуманоида.
    /// </summary>
    [DataField]
    public float AnimalEfficiency = 0.05f;

    /// <summary>
    /// Эффективность крови трупов относительно живой цели.
    /// </summary>
    [DataField]
    public float CorpseEfficiency = 0.1f;

    /// <summary>
    /// Колющий урон за один успешный укус.
    /// </summary>
    [DataField]
    public float BitePierceDamage = 0.5f;

    /// <summary>
    /// Кровотечение за один успешный укус.
    /// </summary>
    [DataField]
    public float BiteBleedAmount = 1f;

    /// <summary>
    /// How much blood is gained when the target has not yet rotted (less than 30 seconds since death)
    /// </summary>
    [DataField]
    public float Rot0Efficiency = 1.0f;
    /// <summary>
    /// How much blood is gained when the target is at the initial stage of rot (less than 3:30 since death)
    /// </summary>
    [DataField]
    public float Rot1Efficiency = 0.5f;
    /// <summary>
    /// How much blood is gained when the target is at the mid stage of rot (less than 6:45 since death)
    /// </summary>
    [DataField]
    public float Rot2Efficiency = 0.25f;
    /// <summary>
    /// How much blood is gained when the target is at the late stage of rot (less than 10:00 since death)
    /// </summary>
    [DataField]
    public float Rot3Efficiency = 0.1f;
    /// <summary>
    /// How much blood is gained when the target is fully rotted (more than 10:00 since death)
    /// </summary>
    [DataField]
    public float Rot4Efficiency = 0.0f;
    /// <summary>
    /// How far a target may be for biting to work
    /// </summary>
    [DataField]
    public float BiteDistanceThreshold = 1.5f;

    /// <summary>
    /// Current blood fullness used instead of normal food needs.
    /// </summary>
    [AutoNetworkedField]
    public float BloodFullness = 90f;

    /// <summary>
    /// Max amount of blood which can be drained from one person.
    /// </summary>
    [DataField]
    public float MaxBloodFullness = 200f;

    /// <summary>
    /// Decay rate per second for blood fullness.
    /// </summary>
    [DataField]
    public float FullnessDecayPerSecond = 0.15f;

    /// <summary>
    /// When <see cref="BloodFullness"/> is empty, apply a movement slowdown.
    /// </summary>
    [DataField]
    public float StarvationWalkSpeedModifier = 0.7f;
    [DataField]
    public float StarvationSprintSpeedModifier = 0.7f;

    /// <summary>
    /// When <see cref="BloodFullness"/> is empty, drain this much <see cref="DrunkBlood"/> per second.
    /// </summary>
    [DataField]
    public int StarvationDrunkBloodDrainPerSecond = 2;

    /// <summary>
    /// Action entities of the vampire, used as ActionId -> EntityUid.
    /// </summary>
    public Dictionary<EntProtoId, EntityUid> ActionEntities = [];

    /// <summary>
    /// Determines whether the vampire is drinking at the moment
    /// </summary>
    public bool IsDrinking = false;

    /// <summary>
    /// tracking how much blood was drunk from each target.
    /// </summary>
    public Dictionary<EntityUid, float> BloodDrunkFromTargets = [];

    [DataField]
    public float MaxBloodPerTarget = 200f;
    [DataField]
    public TimeSpan HolyTickDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public float HolyPlaceRange = 8f;

    /// <summary>
    /// Healing factors
    /// </summary>
    [DataField]
    public float VampHealBurn = 1f;

    [DataField]
    public float VampHealBrute = 1f;

    [DataField]
    public float VampHealAsphyxiation = 4f;

    [DataField]
    public float VampHealPois = 2f;

    [DataField]
    public ProtoId<ReagentPrototype> HolyWaterReagentId = "Holywater";

    [AutoPausedField]
    public TimeSpan NextHolyWaterTick = TimeSpan.Zero;

    [AutoPausedField]
    public TimeSpan NextHolyPlaceTick = TimeSpan.Zero;

    [AutoPausedField]
    public TimeSpan NextHolyPlacePopup = TimeSpan.Zero;

    public float StarvationDrunkBloodDrainAccumulator;

    /// <summary>
    /// Дробная часть накопленной крови, ожидающая преобразования в целые единицы.
    /// </summary>
    [DataField]
    public float DrunkBloodRemainder;

    /// <summary>
    /// Дробная часть крови гуманоидов для прогрессии уровня силы.
    /// </summary>
    [DataField]
    public float TotalBloodRemainder;

    /// <summary>
    /// Количество укусов каждой цели после последнего повреждения глаз.
    /// </summary>
    public Dictionary<EntityUid, int> BiteCountsByTarget = [];

    /// <summary>
    /// Наивысший достигнутый уровень силы. Автоматическая прогрессия никогда не понижает его.
    /// </summary>
    [DataField, AutoNetworkedField]
    public VampirePowerLevel PowerLevel = VampirePowerLevel.Neonate;

    /// <summary>number of Unique victims the vampire has drank from so far</summary>
    [DataField, AutoNetworkedField]
    public int UniqueHumanoidVictims = 0;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextUpdate;

    [AutoPausedField]
    public TimeSpan LastUpdate;

    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(1);
}
