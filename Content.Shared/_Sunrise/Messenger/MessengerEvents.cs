using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Messenger;

[Serializable, NetSerializable]
public sealed class OpenMessengerRequestEvent : EntityEventArgs
{
}

public sealed class MessengerOpenedEvent(EntityUid user, EntityUid pda) : EntityEventArgs
{
    public readonly EntityUid User = user;
    public readonly EntityUid Pda = pda;
}

public sealed class MessengerMessageSentEvent(EntityUid user, string? recipientId, string? groupId) : EntityEventArgs
{
    public readonly EntityUid User = user;
    public readonly string? RecipientId = recipientId;
    public readonly string? GroupId = groupId;
}
