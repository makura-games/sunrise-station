using Content.Shared._Sunrise.Tutorial.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Succeeds after the player clicks a UI control matched by <see cref="Selectors"/>.
/// </summary>
public sealed partial class UiButtonPressedCondition : TutorialConditionBase<UiButtonPressedCondition>
{
    [DataField(required: true)]
    public string Button = string.Empty;

    [DataField(required: true)]
    public List<TutorialUiHighlightSelector> Selectors = [];

    [DataField]
    public int Count = 1;

    public string CounterKey => GetCounterKey(Button);

    public static string GetCounterKey(string button)
    {
        return string.Concat(nameof(UiButtonPressedCondition), ":", button);
    }
}
