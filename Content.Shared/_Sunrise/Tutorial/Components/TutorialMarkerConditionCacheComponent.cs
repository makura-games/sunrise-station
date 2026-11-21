using Content.Shared._Sunrise.Objectives;

namespace Content.Shared._Sunrise.Tutorial.Components;

/// <summary>
/// Хранит на сущности цели найденные учебные маркеры для условий, привязанных к маркерам,
/// чтобы не перебирать все маркеры карты на каждой оценке.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialMarkerConditionCacheComponent : Component
{
    /// <summary>
    /// Кэш маркеров по хэндлу условия.
    /// </summary>
    [ViewVariables]
    public Dictionary<ObjectiveConditionHandle, TutorialMarkerCache> Caches = [];
}

/// <summary>
/// Маркеры одного условия на конкретном гриде туториала.
/// </summary>
public sealed class TutorialMarkerCache
{
    /// <summary>
    /// Грид, для которого собран кэш. Смена грида туториала сбрасывает кэш.
    /// </summary>
    public EntityUid? Grid;

    /// <summary>
    /// Подходящие маркеры на этом гриде.
    /// </summary>
    public readonly List<EntityUid> Markers = [];
}
