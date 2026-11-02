using Content.Shared.Alert;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Metabolism;
using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Настройки общей игровой логики и серверной роли вампира.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VampireConfigurationComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>
    /// Тип метаболизма органов на время вампиризма.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<MetabolizerTypePrototype> MetabolizerType = "Vampire";

    /// <summary>
    /// Роль разума при обращении.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId MindRole = "MindRoleVampire";

    /// <summary>
    /// Фиксированные цели для выдачи при обращении.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> Objectives = new()
    {
        "VampireKillRandomPersonObjective",
        "VampireDrainObjective",
    };

    /// <summary>
    /// Группы для выбора одной цели из каждой.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<WeightedRandomPrototype>> ObjectiveGroups = new()
    {
        "VampireObjectiveGroupsStateOnly",
        "VampireObjectiveGroupsStealOnly",
    };

    [DataField, AutoNetworkedField]
    public float ObjectiveMaxDifficulty = 10f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier BriefingSound =
        new SoundPathSpecifier("/Audio/_Sunrise/Ambience/Antag/vampire_start.ogg");

    /// <summary>
    /// Действия для выдачи через ActionGrant при получении вампиризма.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> BaseActions = new()
    {
        "ActionVampireToggleFangs",
        "ActionVampireGlare",
        "ActionVampireRejuvenateI",
        "ActionVampireSleep",
    };

    [DataField, AutoNetworkedField]
    public EntProtoId FangsAction = "ActionVampireToggleFangs";

    [DataField, AutoNetworkedField]
    public EntProtoId GlareAction = "ActionVampireGlare";

    [DataField, AutoNetworkedField]
    public EntProtoId SleepAction = "ActionVampireSleep";

    /// <summary>
    /// Базовое омоложение, заменяемое улучшенным по уровню силы.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId RejuvenateAction = "ActionVampireRejuvenateI";

    [DataField, AutoNetworkedField]
    public EntProtoId RejuvenateUpgradedAction = "ActionVampireRejuvenateII";

    /// <summary>
    /// Задержка переключения клыков, не глотка.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan FangsUseDelay = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public VampirePowerLevel MaxProgressionLevel = VampirePowerLevel.Ancient;

    /// <summary>
    /// Уровень обхода защиты веры при питании.
    /// </summary>
    [DataField, AutoNetworkedField]
    public VampirePowerLevel FaithProtectionPowerLevel = VampirePowerLevel.Ancient;

    /// <summary>
    /// Порог скалярного произведения для передней зоны взгляда.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float GlareDirectionThreshold = 0.8f;

    /// <summary>
    /// Допустимое смещение при концентрации на усыплении.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SleepMovementThreshold = 0.1f;

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> MuteToxinReagent = "MuteToxin";

    /// <summary>
    /// Стадия метаболизма реагентов, удаляемых омоложением.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<MetabolismStagePrototype> RejuvenatePurgeMetabolismStage = "Poison";

    /// <summary>
    /// Сообщения цели при начале усыпления.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<LocId> SleepTargetMessages = new()
    {
        "vampire-sleep-target-warning-1",
        "vampire-sleep-target-warning-2",
        "vampire-sleep-target-warning-3",
        "vampire-sleep-target-warning-4",
        "vampire-sleep-target-warning-5",
    };

    [DataField, AutoNetworkedField]
    public Dictionary<VampirePowerLevel, LocId> PowerLevelMessages = new()
    {
        [VampirePowerLevel.Awakened] = "vampire-power-awakened-message",
        [VampirePowerLevel.Nightborn] = "vampire-power-nightborn-message",
        [VampirePowerLevel.Ancient] = "vampire-power-ancient-message",
    };

    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> FedAlert = "VampireFed";

    /// <summary>
    /// Категория обычного голода, скрываемая при получении вампиризма.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<AlertCategoryPrototype> HungerAlertCategory = "Hunger";
}
