using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared._Sunrise.Tutorial.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Проверяет, что на клиенте игрока виден указанный UI-контрол.
/// </summary>
public sealed partial class UiControlVisibleObjectiveCondition : ObjectiveEventConditionBase<UiControlVisibleObjectiveCondition>
{
    [DataField(required: true)]
    public string Control = string.Empty;

    [DataField(required: true)]
    public List<TutorialUiHighlightSelector> Selectors = [];

    public override string CounterKey => GetCounterKey(Control);

    public static string GetCounterKey(string control)
    {
        return string.Concat(nameof(UiControlVisibleObjectiveCondition), ":", control);
    }
}
