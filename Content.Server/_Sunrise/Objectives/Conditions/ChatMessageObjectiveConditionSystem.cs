using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared._Sunrise.Objectives.Components;
using Content.Server.Chat.Systems;
using Content.Shared._Sunrise.Chat;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Objectives.Conditions;

public sealed partial class ChatMessageObjectiveConditionSystem
    : ObjectiveEventConditionSystem<ChatMessageObjectiveCondition, ObjectiveCommunicationOwnerComponent, ObjectiveCommunicationObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObjectiveCommunicationOwnerComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<ObjectiveCommunicationOwnerComponent, EntityEmotedEvent>(OnEntityEmoted);
        SubscribeLocalEvent<ObjectiveCommunicationOwnerComponent, InGameOocMessageAttemptEvent>(OnInGameOocMessageAttempt);
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioSpoke);
    }

    private void OnEntitySpoke(Entity<ObjectiveCommunicationOwnerComponent> ent, ref EntitySpokeEvent args)
    {
        if (args.Channel != null)
            return;

        if (args.ObfuscatedMessage != null)
        {
            RecordEvent(ent, ChatMessageObjectiveCondition.GetCounterKey(ObjectiveChatMessageKind.Whisper));
            return;
        }

        RecordEvent(ent, ChatMessageObjectiveCondition.GetCounterKey(ObjectiveChatMessageKind.Local));
    }

    private void OnEntityEmoted(Entity<ObjectiveCommunicationOwnerComponent> ent, ref EntityEmotedEvent args)
    {
        RecordEvent(ent, ChatMessageObjectiveCondition.GetCounterKey(ObjectiveChatMessageKind.Emote));
    }

    private void OnInGameOocMessageAttempt(Entity<ObjectiveCommunicationOwnerComponent> ent, ref InGameOocMessageAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.Type != InGameOOCChatType.Looc)
            return;

        if (args.Session.AttachedEntity != ent.Owner)
            return;

        RecordEvent(ent, ChatMessageObjectiveCondition.GetCounterKey(ObjectiveChatMessageKind.Looc));
    }

    private void OnRadioSpoke(RadioSpokeEvent args)
    {
        if (!HasComp<ObjectiveCommunicationOwnerComponent>(args.Source))
            return;

        var channel = new ProtoId<RadioChannelPrototype>(args.ChannelId);

        RecordEvent(args.Source, ChatMessageObjectiveCondition.GetCounterKey(ObjectiveChatMessageKind.Radio));
        RecordEvent(args.Source, ChatMessageObjectiveCondition.GetCounterKey(ObjectiveChatMessageKind.Radio, channel));

        if (args.ChannelId == "Common")
            return;

        RecordEvent(args.Source, ChatMessageObjectiveCondition.GetCounterKey(ObjectiveChatMessageKind.DepartmentRadio));
        RecordEvent(args.Source, ChatMessageObjectiveCondition.GetCounterKey(ObjectiveChatMessageKind.DepartmentRadio, channel));
    }
}
