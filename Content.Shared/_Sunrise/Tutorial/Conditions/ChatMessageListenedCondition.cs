using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

public sealed partial class ChatMessageListenedCondition : TutorialConditionBase<ChatMessageListenedCondition>
{
    [DataField]
    public TutorialChatMessageKind Kind = TutorialChatMessageKind.Local;

    [DataField]
    public ProtoId<RadioChannelPrototype>? Channel;

    [DataField]
    public int Count = 1;

    public string CounterKey => GetCounterKey(Kind, Channel);

    public static string GetCounterKey(TutorialChatMessageKind kind, ProtoId<RadioChannelPrototype>? channel = null)
    {
        return channel is { } channelId
            ? $"{nameof(ChatMessageListenedCondition)}:{kind}:{channelId.Id}"
            : $"{nameof(ChatMessageListenedCondition)}:{kind}";
    }
}

public enum TutorialChatMessageKind : byte
{
    Local,
    Whisper,
    Emote,
    Looc,
    Radio,
    DepartmentRadio,
}
