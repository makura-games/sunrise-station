using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Content.Shared._Sunrise.Tutorial.Events;

namespace Content.Server._Sunrise.Tutorial.Conditions;

/// <summary>
/// Validates and records client-reported tutorial control visibility.
/// </summary>
public sealed class UiControlVisibleObjectiveConditionSystem :
    ObjectiveEventConditionSystem<UiControlVisibleObjectiveCondition, ObjectiveUiOwnerComponent, ObjectiveUiObserverComponent>
{
    protected override bool Validate(UiControlVisibleObjectiveCondition condition, out string? error)
    {
        error = string.IsNullOrWhiteSpace(condition.Control) || condition.Selectors.Count == 0
            ? "control identifier and selectors are required"
            : null;
        return error == null;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TutorialUiControlVisibleEvent>(OnUiControlVisible);
    }

    private void OnUiControlVisible(TutorialUiControlVisibleEvent message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        TryRecordVisible(player, message.Control);
    }

    /// <summary>
    /// Attempts to record a visible control for an active tutorial objective.
    /// </summary>
    public bool TryRecordVisible(EntityUid player, string control)
    {
        if (!CanRecordVisible(player, control))
            return false;

        return RecordEvent(
            player,
            UiControlVisibleObjectiveCondition.GetCounterKey(control),
            sourceIdentifierPrefix: "Tutorial") > 0;
    }

    /// <summary>
    /// Checks whether the player may report tutorial control visibility.
    /// </summary>
    public bool CanRecordVisible(EntityUid player, string control)
    {
        return !string.IsNullOrWhiteSpace(control) &&
               TryComp(player, out TutorialPlayerComponent? tutorial) &&
               tutorial.TutorialInitialized &&
               HasComp<ObjectiveUiOwnerComponent>(player);
    }
}
