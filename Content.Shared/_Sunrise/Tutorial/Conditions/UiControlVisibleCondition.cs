using Content.Shared._Sunrise.Tutorial.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Проверяет, что на клиенте игрока виден указанный UI-контрол.
/// </summary>
public sealed partial class UiControlVisibleCondition : TutorialConditionBase<UiControlVisibleCondition>
{
    [DataField(required: true)]
    public string Control = string.Empty;

    [DataField(required: true)]
    public List<TutorialUiHighlightSelector> Selectors = [];

    [DataField]
    public int Count = 1;

    public string CounterKey => GetCounterKey(Control);

    public static string GetCounterKey(string control)
    {
        return string.Concat(nameof(UiControlVisibleCondition), ":", control);
    }
}
