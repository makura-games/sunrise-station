using Content.Shared._Sunrise.Antags.Vampires.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Идентичность вампира: класс и выдача способностей.
/// Прогрессия, питьё, лечение, святая вода вынесены в отдельные компоненты:
/// VampireProgressionComponent, VampireBloodDrinkerComponent, VampireHealingComponent, VampireHolyComponent.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class VampireComponent : Component
{
    /// <summary>
    /// Идентификаторы ключевых действий, используемые системами в коде.
    /// </summary>
    public const string ToggleFangsActionId = "ActionVampireToggleFangs";
    public const string GlareActionId = "ActionVampireGlare";
    public const string RejuvenateIActionId = "ActionVampireRejuvenateI";
    public const string RejuvenateIIActionId = "ActionVampireRejuvenateII";
    public const string BloodBringersRiteActionId = "ActionVampireBloodBringersRite";
    public const string CloakOfDarknessActionId = "ActionVampireCloakOfDarkness";
    public const string ExtinguishActionId = "ActionVampireExtinguish";
    public const string EternalDarknessActionId = "ActionVampireEternalDarkness";

    /// <summary>
    /// Идентификатор прототипа выбранного класса вампира.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<VampireClassPrototype>? ChosenClassId;

    /// <summary>
    /// Базовые способности, добавляются при старте.
    /// </summary>
    [DataField]
    public List<EntProtoId> BaseVampireActions = new()
    {
        "ActionVampireToggleFangs",
        "ActionVampireGlare",
        "ActionVampireRejuvenateI",
        "ActionVampireSleep"
    };

    /// <summary>
    /// Ключевые идентификаторы действий, которыми системы управляют явно.
    /// </summary>
    [DataField]
    public EntProtoId ClassSelectActionId = "ActionClassSelectId";

    /// <summary>
    /// Пара действий Омоложения: базовое и улучшенное (I, II).
    /// </summary>
    [DataField]
    public List<EntProtoId> RejuvenateActions = new()
    {
        "ActionVampireRejuvenateI",
        "ActionVampireRejuvenateII"
    };

    /// <summary>
    /// Сущности действий вампира: ActionId -> EntityUid.
    /// </summary>
    public Dictionary<EntProtoId, EntityUid> ActionEntities = new();

    /// <summary>
    /// Текущая созданная сущность вампирских когтей, если есть.
    /// </summary>
    public EntityUid? SpawnedClaws = null;
}
