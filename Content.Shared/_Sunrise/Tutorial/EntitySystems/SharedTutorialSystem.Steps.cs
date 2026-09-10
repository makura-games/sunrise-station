using System.Diagnostics.CodeAnalysis;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems;

public abstract partial class SharedTutorialSystem
{
    /// <summary>
    /// Gets the currently active step prototype for a tutorial player.
    /// </summary>
    public bool TryGetCurrentStep(Entity<TutorialPlayerComponent> ent, [NotNullWhen(true)] out TutorialStepPrototype? step)
    {
        step = null;

        if (ent.Comp.ActiveStepOverride is { } overrideStep)
            return _proto.TryIndex(overrideStep, out step);

        return TryGetSequenceStep(ent, out step);
    }

    private bool TryGetSequenceStep(Entity<TutorialPlayerComponent> ent, [NotNullWhen(true)] out TutorialStepPrototype? step)
    {
        step = null;

        if (!_proto.TryIndex(ent.Comp.SequenceId, out var sequence))
            return false;

        if (ent.Comp.StepIndex < 0 || ent.Comp.StepIndex >= sequence.Steps.Count)
            return false;

        return _proto.TryIndex(sequence.Steps[ent.Comp.StepIndex], out step);
    }

    private bool TryEnterRepairStep(Entity<TutorialPlayerComponent> ent, TutorialStepPrototype step)
    {
        for (var i = 0; i < step.Failures.Count; i++)
        {
            var failure = step.Failures[i];
            if (!IsFailureRuleMet(ent, i))
                continue;

            return EnterRepairStep(ent, failure.RepairStep);
        }

        return false;
    }

    private bool IsFailureRuleMet(Entity<TutorialPlayerComponent> ent, int index)
    {
        return TryComp(ent, out TutorialStepObjectivesComponent? objectives) &&
               index >= 0 &&
               index < objectives.FailuresSatisfied.Count &&
               objectives.FailuresSatisfied[index];
    }

    private bool EnterRepairStep(Entity<TutorialPlayerComponent> ent, ProtoId<TutorialStepPrototype> repairStepId)
    {
        if (!_proto.TryIndex(repairStepId, out var repairStep))
            return false;

        ClearActiveStepState(ent);
        ent.Comp.ActiveStepOverride = repairStepId;
        Dirty(ent, ent.Comp);
        OnStepChanged(ent, repairStep);
        return true;
    }

    private void ReturnFromRepairStep(Entity<TutorialPlayerComponent> ent)
    {
        ClearActiveStepState(ent);
        ent.Comp.ActiveStepOverride = null;
        Dirty(ent, ent.Comp);

        if (TryGetSequenceStep(ent, out var step))
            OnStepChanged(ent, step);
    }

    private void ClearActiveStepState(Entity<TutorialPlayerComponent> ent)
    {
        ClearTracking(ent);
        ClearStepEffects(ent);
        ClearTutorialBubble(ent);
    }

    /// <summary>
    /// Tries to resolve an entity's prototype ID for condition target matching.
    /// </summary>
    public bool TryGetPrototypeId(EntityUid? uid, out EntProtoId protoId)
    {
        protoId = default;

        if (uid is not { } target)
            return false;

        var proto = Prototype(target);
        if (proto == null)
            return false;

        protoId = proto.ID;
        return true;
    }
}
