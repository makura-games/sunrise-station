using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Tutorial.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Находит и кэширует учебные маркеры для условий целей, привязанных к маркерам.
/// Маркеры — статичные сущности карты туториала, поэтому достаточно найти их один раз на условие.
/// </summary>
public sealed partial class TutorialMarkerConditionCacheSystem : EntitySystem
{
    /// <summary>
    /// Возвращает маркеры нужного прототипа на гриде туториала. Кэш пересобирается,
    /// если грид сменился или маркеров ещё не нашлось. Список может содержать удалённые сущности.
    /// </summary>
    public IReadOnlyList<EntityUid> GetMarkers(ObjectiveConditionContext context, EntProtoId? marker, EntityUid? grid)
    {
        var component = EnsureComp<TutorialMarkerConditionCacheComponent>(context.Objective);
        if (!component.Caches.TryGetValue(context.Handle, out var cache))
        {
            cache = new TutorialMarkerCache();
            component.Caches.Add(context.Handle, cache);
        }

        if (cache.Grid == grid && cache.Markers.Count > 0)
            return cache.Markers;

        cache.Grid = grid;
        cache.Markers.Clear();

        var query = EntityQueryEnumerator<TutorialGoalMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            if (marker is { } markerId && markerId.Id != Prototype(uid)?.ID)
                continue;

            cache.Markers.Add(uid);
        }

        return cache.Markers;
    }

    /// <summary>
    /// Удаляет кэш условия при его деактивации.
    /// </summary>
    public void Release(ObjectiveConditionContext context)
    {
        if (!TryComp(context.Objective, out TutorialMarkerConditionCacheComponent? component))
            return;

        component.Caches.Remove(context.Handle);
        if (component.Caches.Count == 0 && !TerminatingOrDeleted(context.Objective))
            RemComp<TutorialMarkerConditionCacheComponent>(context.Objective);
    }
}
