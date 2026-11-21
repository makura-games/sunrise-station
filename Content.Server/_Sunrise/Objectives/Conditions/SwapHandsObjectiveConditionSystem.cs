using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared.Hands;
using Content.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.Objectives.Conditions;

/// <summary>
/// Fires when the player switches their active hand via key binding or GUI,
/// regardless of whether any item is held.
/// </summary>
public sealed partial class SwapHandsObjectiveConditionSystem : ObjectiveEventConditionSystem<SwapHandsObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        // Клик по кнопке руки приходит сетевым RequestSetHandEvent, а не направленным событием сущности.
        SubscribeAllEvent<RequestSetHandEvent>(OnRequestSetHand);

        // Горячие клавиши SwapHands обрабатываются на сервере через InputCmdHandler.
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.SwapHands,
                InputCmdHandler.FromDelegate(OnSwapHandsKey, handle: false, outsidePrediction: false))
            .Bind(ContentKeyFunctions.SwapHandsReverse,
                InputCmdHandler.FromDelegate(OnSwapHandsKey, handle: false, outsidePrediction: false))
            .Register<SwapHandsObjectiveConditionSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<SwapHandsObjectiveConditionSystem>();
    }

    private void OnRequestSetHand(RequestSetHandEvent msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (player == null || !HasComp<ObjectiveInteractionOwnerComponent>(player.Value))
            return;

        RecordEvent(player.Value, DefaultKey, player.Value);
    }

    private void OnSwapHandsKey(ICommonSession? session)
    {
        var player = session?.AttachedEntity;
        if (player == null || !HasComp<ObjectiveInteractionOwnerComponent>(player.Value))
            return;

        RecordEvent(player.Value, DefaultKey, player.Value);
    }
}
