using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Tutorial.Conditions;

public sealed partial class StepDelayConditionSystem
    : TutorialConditionSystem<TutorialPlayerComponent, StepDelayCondition>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Condition(Entity<TutorialPlayerComponent> entity, ref TutorialConditionEvent<StepDelayCondition> args)
    {
        if (args.Condition.Delay <= TimeSpan.Zero)
        {
            args.Result = true;
            return;
        }

        args.Result = _timing.CurTime >= entity.Comp.StepStartedAt + args.Condition.Delay;
    }
}
