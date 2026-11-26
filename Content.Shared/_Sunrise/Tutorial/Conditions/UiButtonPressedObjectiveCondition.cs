using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared._Sunrise.Tutorial.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Succeeds after the player clicks a UI control matched by <see cref="Selectors"/>.
/// </summary>
public sealed partial class UiButtonPressedObjectiveCondition : ObjectiveEventConditionBase<UiButtonPressedObjectiveCondition>
{
    [DataField(required: true)]
    public string Button = string.Empty;

    [DataField(required: true)]
    public List<TutorialUiHighlightSelector> Selectors = [];

    public override string CounterKey => GetCounterKey(Button);

    public static string GetCounterKey(string button)
    {
        return string.Concat(nameof(UiButtonPressedObjectiveCondition), ":", button);
    }
}
