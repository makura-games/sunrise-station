using Content.Shared._Sunrise.Objectives;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Checks if the player has opened a physical storage container (locker, crate, etc.).
/// For bag/backpack BUI opens use <see cref="BuiOpenObjectiveCondition"/> instead.
/// Supports any storage or a specific prototype via <see cref="ObjectiveEventConditionBase{StorageOpenObjectiveCondition}.Target"/>.
/// </summary>
public sealed partial class StorageOpenObjectiveCondition : ObjectiveEventConditionBase<StorageOpenObjectiveCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}
