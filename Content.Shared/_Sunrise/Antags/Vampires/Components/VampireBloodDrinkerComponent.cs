using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Механика питья крови. Вынесена из VampireComponent отдельно,
/// чтобы переиспользоваться не только вампирами.
/// Эффективность крови цели определяется BloodSourceComponent на цели,
/// а не жёсткими коэффициентами здесь.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class VampireBloodDrinkerComponent : Component
{
    /// <summary>
    /// Определяет, выдвинуты ли клыки.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), DataField, AutoNetworkedField]
    public bool FangsExtended = false;

    /// <summary>
    /// Определяет, пьёт ли вампир кровь в данный момент
    /// </summary>
    public bool IsDrinking = false;

    /// <summary>
    /// отслеживает, сколько крови выпито из каждой цели.
    /// </summary>
    public Dictionary<EntityUid, int> BloodDrunkFromTargets = new();

    /// <summary>
    /// Максимум крови из одной цели до её опустошения.
    /// </summary>
    [DataField]
    public int MaxBloodPerTarget = 200;

    /// <summary>
    /// объём крови в единицах, потребляемый вампиром за укус
    /// </summary>
    [DataField]
    public float SipAmount = 10f;

    /// <summary>
    /// урон за 1 единицу крови, вытянутую из цели
    /// </summary>
    [DataField]
    public float SipPierceDamage = 0.05f;

    /// <summary>
    /// Максимальная дистанция до цели для укуса
    /// </summary>
    [DataField]
    public float BiteDistanceThreshold = 1.5f;

    /// <summary>
    /// Звук укуса жертвы.
    /// </summary>
    [DataField]
    public SoundSpecifier BiteSound = new SoundPathSpecifier("/Audio/Effects/bite.ogg");

    /// <summary>
    /// Текущая сытость кровью вместо обычной потребности в еде.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), DataField, AutoNetworkedField]
    public float BloodFullness = 90f;

    /// <summary>
    /// Максимальный объём крови, выпиваемый из одного человека.
    /// </summary>
    [DataField]
    public float MaxBloodFullness = 200f;

    /// <summary>
    /// Скорость убывания сытости кровью в секунду.
    /// </summary>
    [DataField]
    public float FullnessDecayPerSecond = 0.15f;

    /// <summary>
    /// Когда <see cref="BloodFullness"/> пуст, применяется замедление движения.
    /// </summary>
    [DataField]
    public float StarvationWalkSpeedModifier = 0.7f;

    /// <summary>
    /// Множитель скорости спринта при голодании.
    /// </summary>
    [DataField]
    public float StarvationSprintSpeedModifier = 0.7f;

    /// <summary>
    /// Когда <see cref="BloodFullness"/> пуст, расходуется столько запасённой крови в секунду.
    /// </summary>
    [DataField]
    public int StarvationDrunkBloodDrainPerSecond = 2;

    /// <summary>
    /// Накопитель для расхода запасённой крови при голодании.
    /// </summary>
    public float StarvationDrunkBloodDrainAccumulator;
}
