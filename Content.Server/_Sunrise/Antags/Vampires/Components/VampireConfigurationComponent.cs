using Content.Shared._Sunrise.Antags.Vampires;
using Content.Shared.Alert;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Metabolism;
using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Серверная конфигурация вампира.
/// </summary>
[RegisterComponent]
public sealed partial class VampireConfigurationComponent : Component
{
    /// <summary>
    /// Тип метаболизма.
    /// </summary>
    [DataField]
    public ProtoId<MetabolizerTypePrototype> MetabolizerType = "Vampire";

    /// <summary>
    /// Роль вампира.
    /// </summary>
    [DataField]
    public EntProtoId MindRole = "MindRoleVampire";

    /// <summary>
    /// Обязательные цели.
    /// </summary>
    [DataField]
    public List<EntProtoId> Objectives =
    [
        "VampireKillRandomPersonObjective",
        "VampireDrainObjective",
    ];

    /// <summary>
    /// Группы случайных целей.
    /// </summary>
    [DataField]
    public List<ProtoId<WeightedRandomPrototype>> ObjectiveGroups =
    [
        "VampireObjectiveGroupsStateOnly",
        "VampireObjectiveGroupsStealOnly",
    ];

    /// <summary>
    /// Предел сложности случайной цели.
    /// </summary>
    [DataField]
    public float ObjectiveMaxDifficulty = 10f;

    /// <summary>
    /// Звук вступления.
    /// </summary>
    [DataField]
    public SoundSpecifier BriefingSound =
        new SoundPathSpecifier("/Audio/_Sunrise/Ambience/Antag/vampire_start.ogg");

    /// <summary>
    /// Базовые действия.
    /// </summary>
    [DataField]
    public List<EntProtoId> BaseActions =
    [
        "ActionVampireToggleFangs",
        "ActionVampireGlare",
        "ActionVampireRejuvenateI",
        "ActionVampireSleep",
    ];

    /// <summary>
    /// Действие клыков.
    /// </summary>
    [DataField]
    public EntProtoId FangsAction = "ActionVampireToggleFangs";

    /// <summary>
    /// Действие взгляда.
    /// </summary>
    [DataField]
    public EntProtoId GlareAction = "ActionVampireGlare";

    /// <summary>
    /// Действие сна.
    /// </summary>
    [DataField]
    public EntProtoId SleepAction = "ActionVampireSleep";

    /// <summary>
    /// Первое омоложение.
    /// </summary>
    [DataField]
    public EntProtoId RejuvenateAction = "ActionVampireRejuvenateI";

    /// <summary>
    /// Улучшенное омоложение.
    /// </summary>
    [DataField]
    public EntProtoId RejuvenateUpgradedAction = "ActionVampireRejuvenateII";

    /// <summary>
    /// Задержка клыков.
    /// </summary>
    [DataField]
    public TimeSpan FangsUseDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Максимальный уровень обычной прогрессии.
    /// </summary>
    [DataField]
    public VampirePowerLevel MaxProgressionLevel = VampirePowerLevel.Ancient;

    /// <summary>
    /// Уровень обхода защиты веры.
    /// </summary>
    [DataField]
    public VampirePowerLevel FaithProtectionPowerLevel = VampirePowerLevel.Ancient;

    /// <summary>
    /// Порог направления взгляда.
    /// </summary>
    [DataField]
    public float GlareDirectionThreshold = 0.8f;

    /// <summary>
    /// Допустимое движение во время сна.
    /// </summary>
    [DataField]
    public float SleepMovementThreshold = 0.1f;

    /// <summary>
    /// Токсин немоты.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> MuteToxinReagent = "MuteToxin";

    /// <summary>
    /// Группа реагентов для очищения.
    /// </summary>
    [DataField]
    public ProtoId<MetabolismStagePrototype> RejuvenatePurgeMetabolismStage = "Poison";

    /// <summary>
    /// Предупреждения цели сна.
    /// </summary>
    [DataField]
    public List<LocId> SleepTargetMessages =
    [
        "vampire-sleep-target-warning-1",
        "vampire-sleep-target-warning-2",
        "vampire-sleep-target-warning-3",
        "vampire-sleep-target-warning-4",
        "vampire-sleep-target-warning-5",
    ];

    /// <summary>
    /// Сообщения о новых уровнях силы.
    /// </summary>
    [DataField]
    public Dictionary<VampirePowerLevel, LocId> PowerLevelMessages = new()
    {
        [VampirePowerLevel.Awakened] = "vampire-power-awakened-message",
        [VampirePowerLevel.Nightborn] = "vampire-power-nightborn-message",
        [VampirePowerLevel.Ancient] = "vampire-power-ancient-message",
    };

    /// <summary>
    /// Индикатор сытости.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> FedAlert = "VampireFed";

    /// <summary>
    /// Категория обычного голода.
    /// </summary>
    [DataField]
    public ProtoId<AlertCategoryPrototype> HungerAlertCategory = "Hunger";
}
