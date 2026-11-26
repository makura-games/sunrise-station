using Content.Shared._Sunrise.Objectives;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

public sealed partial class ChatMessageObjectiveCondition : ObjectiveEventConditionBase<ChatMessageObjectiveCondition>
{
    [DataField]
    public ObjectiveChatMessageKind Kind = ObjectiveChatMessageKind.Local;

    [DataField]
    public ProtoId<RadioChannelPrototype>? Channel;

    public override string CounterKey => GetCounterKey(Kind, Channel);

    public static string GetCounterKey(ObjectiveChatMessageKind kind, ProtoId<RadioChannelPrototype>? channel = null)
    {
        return channel is { } channelId
            ? $"{nameof(ChatMessageObjectiveCondition)}:{kind}:{channelId.Id}"
            : $"{nameof(ChatMessageObjectiveCondition)}:{kind}";
    }
}

public enum ObjectiveChatMessageKind : byte
{
    Local,
    Whisper,
    Emote,
    Looc,
    Radio,
    DepartmentRadio,
}
