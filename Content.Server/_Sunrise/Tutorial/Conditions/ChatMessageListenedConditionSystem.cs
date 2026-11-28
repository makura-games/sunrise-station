using Content.Server.Chat.Systems;
using Content.Shared._Sunrise.Chat;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Components.Trackers;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Tutorial.Conditions;

public sealed partial class ChatMessageListenedConditionSystem
    : TutorialConditionSystem<TutorialPlayerComponent, ChatMessageListenedCondition>
{
    protected override void Condition(Entity<TutorialPlayerComponent> entity, ref TutorialConditionEvent<ChatMessageListenedCondition> args)
    {
        if (args.Condition.Count <= 0)
        {
            args.Result = true;
            return;
        }

        if (!TryComp<TutorialTrackerComponent>(entity, out var tracker))
            return;

        args.Result = tracker.Counters.TryGetValue((args.Condition.CounterKey, default), out var count) &&
                      count >= args.Condition.Count;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialPlayerComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<TutorialPlayerComponent, EntityEmotedEvent>(OnEntityEmoted);
        SubscribeLocalEvent<TutorialPlayerComponent, InGameOocMessageAttemptEvent>(OnInGameOocMessageAttempt);
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioSpoke);
    }

    private void OnEntitySpoke(Entity<TutorialPlayerComponent> ent, ref EntitySpokeEvent args)
    {
        if (!ent.Comp.TutorialInitialized)
            return;

        if (args.Channel != null)
            return;

        if (args.ObfuscatedMessage != null)
        {
            IncrementCounter(ent, ChatMessageListenedCondition.GetCounterKey(TutorialChatMessageKind.Whisper));
            return;
        }

        IncrementCounter(ent, ChatMessageListenedCondition.GetCounterKey(TutorialChatMessageKind.Local));
    }

    private void OnEntityEmoted(Entity<TutorialPlayerComponent> ent, ref EntityEmotedEvent args)
    {
        if (!ent.Comp.TutorialInitialized)
            return;

        IncrementCounter(ent, ChatMessageListenedCondition.GetCounterKey(TutorialChatMessageKind.Emote));
    }

    private void OnInGameOocMessageAttempt(Entity<TutorialPlayerComponent> ent, ref InGameOocMessageAttemptEvent args)
    {
        if (!ent.Comp.TutorialInitialized || args.Cancelled)
            return;

        if (args.Type != InGameOOCChatType.Looc)
            return;

        if (args.Session.AttachedEntity != ent.Owner)
            return;

        IncrementCounter(ent, ChatMessageListenedCondition.GetCounterKey(TutorialChatMessageKind.Looc));
    }

    private void OnRadioSpoke(RadioSpokeEvent args)
    {
        if (!TryComp(args.Source, out TutorialPlayerComponent? tutorialPlayer) ||
            !tutorialPlayer.TutorialInitialized)
        {
            return;
        }

        var ent = (args.Source, tutorialPlayer);
        var channel = new ProtoId<RadioChannelPrototype>(args.ChannelId);

        IncrementCounter(ent, ChatMessageListenedCondition.GetCounterKey(TutorialChatMessageKind.Radio));
        IncrementCounter(ent, ChatMessageListenedCondition.GetCounterKey(TutorialChatMessageKind.Radio, channel));

        if (args.ChannelId == "Common")
            return;

        IncrementCounter(ent, ChatMessageListenedCondition.GetCounterKey(TutorialChatMessageKind.DepartmentRadio));
        IncrementCounter(ent, ChatMessageListenedCondition.GetCounterKey(TutorialChatMessageKind.DepartmentRadio, channel));
    }

    private void IncrementCounter(Entity<TutorialPlayerComponent> ent, string key)
    {
        var tracker = EnsureComp<TutorialTrackerComponent>(ent);
        var counterKey = (key, default(EntProtoId));
        tracker.Counters.TryGetValue(counterKey, out var count);
        tracker.Counters[counterKey] = count + 1;
        Dirty(ent, tracker);
    }
}
