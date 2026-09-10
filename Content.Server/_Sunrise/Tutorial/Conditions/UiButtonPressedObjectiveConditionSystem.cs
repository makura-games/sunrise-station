using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Content.Shared._Sunrise.Tutorial.Events;

namespace Content.Server._Sunrise.Tutorial.Conditions;

/// <summary>
/// Validates and records client-reported tutorial button interactions.
/// </summary>
public sealed class UiButtonPressedObjectiveConditionSystem :
    ObjectiveEventConditionSystem<UiButtonPressedObjectiveCondition, ObjectiveUiOwnerComponent, ObjectiveUiObserverComponent>
{
    protected override bool Validate(UiButtonPressedObjectiveCondition condition, out string? error)
    {
        error = string.IsNullOrWhiteSpace(condition.Button) || condition.Selectors.Count == 0
            ? "button identifier and selectors are required"
            : null;
        return error == null;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TutorialUiButtonPressedEvent>(OnUiButtonPressed);
    }

    private void OnUiButtonPressed(TutorialUiButtonPressedEvent message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        TryRecordButton(player, message.Button);
    }

    /// <summary>
    /// Attempts to record a button interaction for an active tutorial objective.
    /// </summary>
    public bool TryRecordButton(EntityUid player, string button)
    {
        if (!CanRecordButton(player, button))
            return false;

        return RecordEvent(
            player,
            UiButtonPressedObjectiveCondition.GetCounterKey(button),
            sourceIdentifierPrefix: "Tutorial") > 0;
    }

    /// <summary>
    /// Checks whether the player may report a tutorial button interaction.
    /// </summary>
    public bool CanRecordButton(EntityUid player, string button)
    {
        return !string.IsNullOrWhiteSpace(button) &&
               TryComp(player, out TutorialPlayerComponent? tutorial) &&
               tutorial.TutorialInitialized &&
               HasComp<ObjectiveUiOwnerComponent>(player);
    }
}
