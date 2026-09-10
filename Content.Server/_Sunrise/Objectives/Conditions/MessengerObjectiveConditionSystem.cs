using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared._Sunrise.Objectives.Components;
using Content.Server._Sunrise.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared._Sunrise.Messenger;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Objectives.Conditions;

public sealed partial class MessengerOpenedObjectiveConditionSystem
    : ObjectiveEventConditionSystem<MessengerOpenedObjectiveCondition, ObjectiveCommunicationOwnerComponent, ObjectiveCommunicationObserverComponent>
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObjectiveCommunicationOwnerComponent, MessengerOpenedEvent>(OnMessengerOpened);
    }

    protected override void Evaluate(
        ObjectiveConditionContext context,
        MessengerOpenedObjectiveCondition condition,
        ref ObjectiveConditionEvaluateEvent<MessengerOpenedObjectiveCondition> args)
    {
        args.Satisfied = IsMessengerOpen(context.Owner);
    }

    private void OnMessengerOpened(Entity<ObjectiveCommunicationOwnerComponent> ent, ref MessengerOpenedEvent args)
    {
        RecordEvent(ent, nameof(MessengerOpenedObjectiveCondition));
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

public sealed partial class MessengerMessageObjectiveConditionSystem
    : ObjectiveEventConditionSystem<MessengerMessageObjectiveCondition, ObjectiveCommunicationOwnerComponent, ObjectiveCommunicationObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObjectiveCommunicationOwnerComponent, MessengerMessageSentEvent>(OnMessengerMessageSent);
    }

    private void OnMessengerMessageSent(Entity<ObjectiveCommunicationOwnerComponent> ent, ref MessengerMessageSentEvent args)
    {
        RecordEvent(ent, MessengerMessageObjectiveCondition.GetCounterKey());

        if (!string.IsNullOrWhiteSpace(args.GroupId))
            RecordEvent(ent, MessengerMessageObjectiveCondition.GetCounterKey(groupId: args.GroupId));

        if (!string.IsNullOrWhiteSpace(args.RecipientId))
            RecordEvent(ent, MessengerMessageObjectiveCondition.GetCounterKey(recipientId: args.RecipientId));
    }
}
