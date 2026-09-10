using Content.Shared._Sunrise.Objectives;

namespace Content.Shared._Sunrise.Objectives.Conditions;

public sealed partial class MessengerOpenedObjectiveCondition
    : ObjectiveEventConditionBase<MessengerOpenedObjectiveCondition>
{
    public override string CounterKey => nameof(MessengerOpenedObjectiveCondition);

    public override bool GetRawSatisfied(int progress, bool reportedSatisfied)
    {
        return progress >= Count || Count <= 1 && reportedSatisfied;
    }
}

public sealed partial class MessengerMessageObjectiveCondition
    : ObjectiveEventConditionBase<MessengerMessageObjectiveCondition>
{
    [DataField]
    public string? GroupId;

    [DataField]
    public string? RecipientId;

    public override string CounterKey => GetCounterKey(GroupId, RecipientId);

    public static string GetCounterKey(string? groupId = null, string? recipientId = null)
    {
        if (!string.IsNullOrWhiteSpace(groupId))
            return $"{nameof(MessengerMessageObjectiveCondition)}:Group:{groupId}";

        if (!string.IsNullOrWhiteSpace(recipientId))
            return $"{nameof(MessengerMessageObjectiveCondition)}:Recipient:{recipientId}";

        return nameof(MessengerMessageObjectiveCondition);
    }
}
