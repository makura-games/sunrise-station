using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Events;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems;

public abstract partial class SharedTutorialSystem
{
    /// <summary>
    /// Пытается пропустить текущий шаг туториала, используя штатный переход к следующему шагу.
    /// </summary>
    public bool TrySkipCurrentStep(Entity<TutorialPlayerComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!CanSkipCurrentStep((ent.Owner, ent.Comp)))
            return false;

        Advance((ent.Owner, ent.Comp));
        return true;
    }

    /// <summary>
    /// Проверяет, можно ли пропустить текущий шаг туториала.
    /// </summary>
    public bool CanSkipCurrentStep(Entity<TutorialPlayerComponent> ent)
    {
        return !TerminatingOrDeleted(ent) &&
               ent.Comp.TutorialInitialized &&
               TryGetCurrentStep(ent, out _);
    }

    private void CheckCondition(Entity<TutorialPlayerComponent> ent)
    {
        if (!TryGetCurrentStep(ent, out var step))
            return;

        if (ent.Comp.ActiveStepOverride == null && TryEnterRepairStep(ent, step))
            return;

        if (!ArePreconditionsSatisfied(ent, step))
        {
            // A missing fail step intentionally means "skip this step".
            Advance(ent, step.PreconditionFailStep);
            return;
        }

        if (!IsCompletionSatisfied(ent))
            return;

        if (ent.Comp.ActiveStepOverride != null)
        {
            ReturnFromRepairStep(ent);
            return;
        }

        Advance(ent);
    }

    private bool IsCompletionSatisfied(Entity<TutorialPlayerComponent> ent)
    {
        return TryComp(ent, out TutorialStepObjectivesComponent? objectives) &&
               objectives.CompletionSatisfied;
    }

    private bool ArePreconditionsSatisfied(
        Entity<TutorialPlayerComponent> ent,
        TutorialStepPrototype step)
    {
        if (step.Preconditions == null)
            return true;

        return TryComp(ent, out TutorialStepObjectivesComponent? objectives) &&
               objectives.PreconditionsSatisfied;
    }

    private void Advance(Entity<TutorialPlayerComponent> ent, ProtoId<TutorialStepPrototype>? stepId = null)
    {
        if (!_proto.TryIndex(ent.Comp.SequenceId, out var sequence))
            return;

        if (stepId == null)
        {
            var nextIndex = ent.Comp.StepIndex + 1;

            ClearActiveStepState(ent);
            ent.Comp.ActiveStepOverride = null;
            UpdateProgressBar(ent, nextIndex);

            if (nextIndex >= sequence.Steps.Count)
            {
                CompleteTutorial(ent, sequence);
                return;
            }

            stepId = sequence.Steps[nextIndex];
        }
        else
        {
            var index = sequence.Steps.IndexOf(stepId.Value);
            if (index < 0 || ent.Comp.StepIndex == index)
                return;

            ClearActiveStepState(ent);
            ent.Comp.ActiveStepOverride = null;
            UpdateProgressBar(ent, index);
        }

        if (!_proto.TryIndex(stepId.Value, out var step))
            return;

        OnStepChanged(ent, step);
    }

    private void UpdateProgressBar(Entity<TutorialPlayerComponent> ent, int index)
    {
        var progressBar = EnsureComp<TutorialProgressBarComponent>(ent);
        progressBar.CurrentStepIndex = index;
        ent.Comp.StepIndex = index;
        Dirty(ent, progressBar);
        Dirty(ent, ent.Comp);
    }

    private void OnStepChanged(Entity<TutorialPlayerComponent> ent, TutorialStepPrototype step)
    {
        ClearTutorialBubble(ent);
        ent.Comp.Target = null;
        ent.Comp.StepStartedAt = _timing.CurTime;
        Dirty(ent, ent.Comp);

        RaiseLocalEvent(ent, new TutorialStepChangedEvent());

        if (_net.IsServer)
        {
            var activatedEvent = new TutorialStepActivatedEvent(ent, step.ID);
            RaiseLocalEvent(ref activatedEvent);
        }

        if (ArePreconditionsSatisfied(ent, step))
        {
            UpdateTutorialBubble(ent, step);
            ApplyStepEffects(ent, step);
        }
    }

    /// <summary>
    /// Stops the tutorial session and clears all tutorial-only runtime state from the player.
    /// </summary>
    public void EndTutorial(Entity<TutorialPlayerComponent> ent)
    {
        ClearStepEffects(ent);
        ClearTutorialBubble(ent);
        ent.Comp.TutorialInitialized = false;
        ent.Comp.ActiveStepOverride = null;
        ent.Comp.Target = null;

        ClearTracking(ent);
        UpdateTimeCounter(ent, null);

        RaiseLocalEvent(ent, new TutorialEndedEvent());
        Dirty(ent);
    }

    /// <summary>
    /// Marks the tutorial sequence as completed and raises completion side-effects.
    /// </summary>
    public void CompleteTutorial(Entity<TutorialPlayerComponent> ent, TutorialSequencePrototype sequence)
    {
        ent.Comp.StepIndex = sequence.Steps.Count;
        ent.Comp.ActiveStepOverride = null;
        ent.Comp.Target = null;

        ClearTutorialBubble(ent);
        ClearTracking(ent);

        RaiseLocalEvent(ent, new TutorialStepsCompletedEvent());
        Dirty(ent, ent.Comp);
    }
}
