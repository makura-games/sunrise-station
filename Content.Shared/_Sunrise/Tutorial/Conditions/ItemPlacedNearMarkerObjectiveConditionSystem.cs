using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared._Sunrise.Tutorial.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Проверяет, что предмет находится в мире рядом с указанным учебным маркером.
/// </summary>
public sealed partial class ItemPlacedNearMarkerObjectiveConditionSystem
    : ObjectiveConditionSystem<TutorialPlayerComponent, ItemPlacedNearMarkerObjectiveCondition>
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _nearbyEntities = [];

    protected override bool Validate(ItemPlacedNearMarkerObjectiveCondition condition, out string? error)
    {
        error = !float.IsFinite(condition.Distance) || condition.Distance <= 0f
            ? "marker distance must be finite and positive"
            : null;
        return error == null;
    }

    protected override void Condition(
        Entity<TutorialPlayerComponent> entity,
        ref ObjectiveConditionEvaluateEvent<ItemPlacedNearMarkerObjectiveCondition> args)
    {
        if (args.Condition.Distance <= 0f)
            return;

        var maximumDistanceSquared = args.Condition.Distance * args.Condition.Distance;
        var markerQuery = EntityQueryEnumerator<TutorialGoalMarkerComponent, TransformComponent>();
        while (markerQuery.MoveNext(out var markerUid, out _, out var markerXform))
        {
            if (!args.Condition.Marker.Equals(Prototype(markerUid)?.ID))
                continue;

            if (markerXform.GridUid != entity.Comp.Grid)
                continue;

            var markerPosition = _transform.GetMapCoordinates(markerUid, xform: markerXform);
            if (markerPosition.MapId == MapId.Nullspace)
                continue;

            _nearbyEntities.Clear();
            _lookup.GetEntitiesInRange(
                markerPosition.MapId,
                markerPosition.Position,
                args.Condition.Distance,
                _nearbyEntities, LookupFlags.Sundries);

            foreach (var target in _nearbyEntities)
            {
                if (!args.Condition.Target.Equals(Prototype(target)?.ID))
                    continue;

                if (_container.IsEntityInContainer(target))
                    continue;

                if (!TryComp(target, out TransformComponent? targetXform) ||
                    targetXform.GridUid != entity.Comp.Grid)
                {
                    continue;
                }

                var targetPosition = _transform.GetMapCoordinates(target, xform: targetXform);
                if (targetPosition.MapId != markerPosition.MapId)
                    continue;

                if ((targetPosition.Position - markerPosition.Position).LengthSquared() > maximumDistanceSquared)
                    continue;

                args.Satisfied = true;
                return;
            }
        }
    }
}

/// <summary>
/// Проверяет текущее положение предмета относительно учебного маркера.
/// Предметы в руках, экипировке и других контейнерах не засчитываются.
/// </summary>
public sealed partial class ItemPlacedNearMarkerObjectiveCondition
    : ObjectiveConditionBase<ItemPlacedNearMarkerObjectiveCondition>
{
    /// <summary>
    /// Прототип предмета, который должен лежать рядом с маркером.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Target;

    /// <summary>
    /// Прототип маркера, рядом с которым должен находиться предмет.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Marker;

    /// <summary>
    /// Максимальное расстояние между центрами предмета и маркера в мировых единицах.
    /// </summary>
    [DataField]
    public float Distance = 2f;
}
