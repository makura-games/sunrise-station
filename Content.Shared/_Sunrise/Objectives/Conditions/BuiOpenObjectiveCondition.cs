using Content.Shared._Sunrise.Objectives;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Checks if the player has opened any bound user interface on an observable entity.
/// Supports any entity or a specific prototype via <see cref="ObjectiveEventConditionBase{BuiOpenObjectiveCondition}.Target"/>.
/// For physical storage containers use <see cref="StorageOpenObjectiveCondition"/> instead.
/// </summary>
public sealed partial class BuiOpenObjectiveCondition : ObjectiveEventConditionBase<BuiOpenObjectiveCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}
