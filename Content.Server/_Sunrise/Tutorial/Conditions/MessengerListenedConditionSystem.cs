using Content.Server._Sunrise.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared._Sunrise.Messenger;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Components.Trackers;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Tutorial.Conditions;

public sealed partial class MessengerOpenedListenedConditionSystem
    : TutorialConditionSystem<TutorialPlayerComponent, MessengerOpenedListenedCondition>
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialPlayerComponent, MessengerOpenedEvent>(OnMessengerOpened);
    }

    protected override void Condition(Entity<TutorialPlayerComponent> entity, ref TutorialConditionEvent<MessengerOpenedListenedCondition> args)
    {
        if (args.Condition.Count <= 0)
        {
            args.Result = true;
            return;
        }

        var count = 0;
        if (TryComp<TutorialTrackerComponent>(entity, out var tracker))
            tracker.Counters.TryGetValue((MessengerOpenedListenedCondition.CounterKey, default), out count);

        if (count == 0 && IsMessengerOpen(entity))
            count = 1;

        args.Result = count >= args.Condition.Count;
    }

    private void OnMessengerOpened(Entity<TutorialPlayerComponent> ent, ref MessengerOpenedEvent args)
    {
        if (!ent.Comp.TutorialInitialized)
            return;

        var tracker = EnsureComp<TutorialTrackerComponent>(ent);
        var key = (MessengerOpenedListenedCondition.CounterKey, default(EntProtoId));
        tracker.Counters.TryGetValue(key, out var count);
        tracker.Counters[key] = count + 1;
        Dirty(ent, tracker);
    }

    private bool IsMessengerOpen(EntityUid user)
    {
        if (!TryFindPda(user, out var pda))
            return false;

        if (pda == null)
            return false;

        if (!_ui.IsUiOpen(pda.Value, PdaUiKey.Key, user))
            return false;

        if (!TryComp(pda, out CartridgeLoaderComponent? loader) ||
            loader.ActiveProgram is not { } activeProgram)
        {
            return false;
        }

        return HasComp<MessengerCartridgeComponent>(activeProgram);
    }

    private bool TryFindPda(EntityUid user, out EntityUid? pda)
    {
        pda = null;

        if (_hands.TryGetActiveItem(user, out var heldItem) && HasComp<PdaComponent>(heldItem))
        {
            pda = heldItem;
            return true;
        }

        if (_inventory.TryGetSlotEntity(user, "id", out var idItem) && HasComp<PdaComponent>(idItem))
        {
            pda = idItem;
            return true;
        }

        foreach (var item in _hands.EnumerateHeld(user))
        {
            if (!HasComp<PdaComponent>(item))
                continue;

            pda = item;
            return true;
        }

        return false;
    }
}

public sealed partial class MessengerMessageListenedConditionSystem
    : TutorialConditionSystem<TutorialPlayerComponent, MessengerMessageListenedCondition>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialPlayerComponent, MessengerMessageSentEvent>(OnMessengerMessageSent);
    }

    protected override void Condition(Entity<TutorialPlayerComponent> entity, ref TutorialConditionEvent<MessengerMessageListenedCondition> args)
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

    private void OnMessengerMessageSent(Entity<TutorialPlayerComponent> ent, ref MessengerMessageSentEvent args)
    {
        if (!ent.Comp.TutorialInitialized)
            return;

        IncrementCounter(ent, MessengerMessageListenedCondition.GetCounterKey());

        if (!string.IsNullOrWhiteSpace(args.GroupId))
            IncrementCounter(ent, MessengerMessageListenedCondition.GetCounterKey(groupId: args.GroupId));

        if (!string.IsNullOrWhiteSpace(args.RecipientId))
            IncrementCounter(ent, MessengerMessageListenedCondition.GetCounterKey(recipientId: args.RecipientId));
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
