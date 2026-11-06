namespace Content.Shared._Sunrise.Tutorial.Conditions;

public sealed partial class MessengerOpenedListenedCondition
    : TutorialConditionBase<MessengerOpenedListenedCondition>
{
    [DataField]
    public int Count = 1;

    public const string CounterKey = nameof(MessengerOpenedListenedCondition);
}

public sealed partial class MessengerMessageListenedCondition
    : TutorialConditionBase<MessengerMessageListenedCondition>
{
    [DataField]
    public string? GroupId;

    [DataField]
    public string? RecipientId;

    [DataField]
    public int Count = 1;

    public string CounterKey => GetCounterKey(GroupId, RecipientId);

    public static string GetCounterKey(string? groupId = null, string? recipientId = null)
    {
        if (!string.IsNullOrWhiteSpace(groupId))
            return $"{nameof(MessengerMessageListenedCondition)}:Group:{groupId}";

        if (!string.IsNullOrWhiteSpace(recipientId))
            return $"{nameof(MessengerMessageListenedCondition)}:Recipient:{recipientId}";

        return nameof(MessengerMessageListenedCondition);
    }
}
