#nullable enable
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

[Serializable, NetSerializable]
public sealed class BwoinkRequestDbMessages : EntityEventArgs
{
    public NetUserId UserId { get; }

    public BwoinkRequestDbMessages(NetUserId userId)
    {
        UserId = userId;
    }
}

[Serializable, NetSerializable]
public sealed class BwoinkTextHistoryMessage : EntityEventArgs
{
    public NetUserId UserId { get; }
    public List<SharedBwoinkSystem.BwoinkTextMessage> Messages { get; }

    public BwoinkTextHistoryMessage(NetUserId userId, List<SharedBwoinkSystem.BwoinkTextMessage> messages)
    {
        UserId = userId;
        Messages = messages;
    }
}

[Serializable, NetSerializable]
public sealed class BwoinkCooldownMessage : EntityEventArgs
{
    public TimeSpan RemainingCooldown { get; }

    public BwoinkCooldownMessage(TimeSpan remainingCooldown)
    {
        RemainingCooldown = remainingCooldown;
    }
}
