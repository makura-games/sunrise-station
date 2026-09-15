using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components.Classes;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class DantalionComponent : VampireClassComponent
{
	/// <summary>
	///     Базовый лимит тхраллов до учёта бонусов за кровь/силу
	/// </summary>
	[DataField]
	public int BaseThrallLimit = 1;

	/// <summary>
	///     Отслеживание порабощённых сущностей во время игры
	/// </summary>
	[ViewVariables(VVAccess.ReadOnly)]
	public HashSet<EntityUid> Thralls = new();

	/// <summary>
	///     Занятые слоты тхраллов. Не уменьшаются при потере тхраллов.
	/// </summary>
	[ViewVariables(VVAccess.ReadOnly)]
	public int ThrallSlotsUsed = 0;

	/// <summary>
	///     Активна ли сейчас Кровавая связь
	/// </summary>
	[ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
	public bool BloodBondActive = false;

	/// <summary>
	///     Идентификатор цикла Кровавой связи против дублирующих циклов
	/// </summary>
	public int BloodBondLoopId = 0;

	/// <summary>
	///     Тхраллы, связанные Кровавой связью
	/// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
	public List<EntityUid> BloodBondLinkedThralls = new();

    /// <summary>
    /// Прототип визуального луча Кровавой связи.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId BloodBondBeamPrototype = string.Empty;

    /// <summary>
    /// Флаг защиты от рекурсии при перераспределении урона Кровавой связи.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool BloodBondProcessingDamage = false;

    /// <summary>
    /// Прототип визуального эффекта призыва тхраллов.
    /// </summary>
    [DataField]
    public EntProtoId RallyOverlayEffect = "VampireRallyOverlayEffect";

    /// <summary>
    /// Порог TotalBlood для второго слота тхраллов.
    /// </summary>
    [DataField]
    public int ThrallLevel2Blood = 400;

    /// <summary>
    /// Порог TotalBlood для третьего слота тхраллов.
    /// </summary>
    [DataField]
    public int ThrallLevel3Blood = 600;

    /// <summary>
    /// Порог TotalBlood, после которого питьё крови лечит тхраллов.
    /// </summary>
    [DataField]
    public int HealBloodThreshold = 300;

    /// <summary>
    /// Группы урона, восстанавливаемые тхраллам при питье крови хозяина.
    /// </summary>
    [DataField]
    public Dictionary<string, int> ThrallHealGroups = new()
    {
        { "Brute", 3 },
        { "Burn", 3 },
    };

    /// <summary>
    /// Типы урона, восстанавливаемые тхраллам при питье крови хозяина.
    /// </summary>
    [DataField]
    public Dictionary<string, int> ThrallHealTypes = new()
    {
        { "Asphyxiation", 5 },
    };
}
