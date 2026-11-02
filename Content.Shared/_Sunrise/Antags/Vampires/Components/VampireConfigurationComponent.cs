using Content.Shared.Alert;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Metabolism;
using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Серверная конфигурация вампира.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VampireConfigurationComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>
    /// Тип метаболизма.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public ProtoId<MetabolizerTypePrototype> MetabolizerType = "Vampire";

    /// <summary>
    /// Роль вампира.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public EntProtoId MindRole = "MindRoleVampire";

    /// <summary>
    /// Обязательные цели.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public List<EntProtoId> Objectives =
    [
        "VampireKillRandomPersonObjective",
        "VampireDrainObjective",
    ];

    /// <summary>
    /// Группы случайных целей.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public List<ProtoId<WeightedRandomPrototype>> ObjectiveGroups =
    [
        "VampireObjectiveGroupsStateOnly",
        "VampireObjectiveGroupsStealOnly",
    ];

    /// <summary>
    /// Предел сложности случайной цели.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public float ObjectiveMaxDifficulty = 10f;

    /// <summary>
    /// Звук вступления.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public SoundSpecifier BriefingSound =
        new SoundPathSpecifier("/Audio/_Sunrise/Ambience/Antag/vampire_start.ogg");

    /// <summary>
    /// Базовые действия.
    /// </summary>
    [AutoNetworkedField]
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
    [AutoNetworkedField]
    [DataField]
    public EntProtoId FangsAction = "ActionVampireToggleFangs";

    /// <summary>
    /// Действие взгляда.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public EntProtoId GlareAction = "ActionVampireGlare";

    /// <summary>
    /// Действие сна.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public EntProtoId SleepAction = "ActionVampireSleep";

    /// <summary>
    /// Первое омоложение.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public EntProtoId RejuvenateAction = "ActionVampireRejuvenateI";

    /// <summary>
    /// Улучшенное омоложение.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public EntProtoId RejuvenateUpgradedAction = "ActionVampireRejuvenateII";

    /// <summary>
    /// Задержка клыков.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public TimeSpan FangsUseDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Максимальный уровень обычной прогрессии.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public VampirePowerLevel MaxProgressionLevel = VampirePowerLevel.Ancient;

    /// <summary>
    /// Уровень обхода защиты веры.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public VampirePowerLevel FaithProtectionPowerLevel = VampirePowerLevel.Ancient;

    /// <summary>
    /// Порог направления взгляда.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public float GlareDirectionThreshold = 0.8f;

    /// <summary>
    /// Допустимое движение во время сна.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public float SleepMovementThreshold = 0.1f;

    /// <summary>
    /// Токсин немоты.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public ProtoId<ReagentPrototype> MuteToxinReagent = "MuteToxin";

    /// <summary>
    /// Группа реагентов для очищения.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public ProtoId<MetabolismStagePrototype> RejuvenatePurgeMetabolismStage = "Poison";

    /// <summary>
    /// Предупреждения цели сна.
    /// </summary>
    [AutoNetworkedField]
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
    [AutoNetworkedField]
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
    [AutoNetworkedField]
    [DataField]
    public ProtoId<AlertPrototype> FedAlert = "VampireFed";

    /// <summary>
    /// Категория обычного голода.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public ProtoId<AlertCategoryPrototype> HungerAlertCategory = "Hunger";
}
