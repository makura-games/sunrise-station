namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Completes a tutorial step after a fixed amount of time.
/// </summary>
public sealed partial class StepDelayCondition : TutorialConditionBase<StepDelayCondition>
{
    /// <summary>
    /// Delay from the moment the step becomes active.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);
}
