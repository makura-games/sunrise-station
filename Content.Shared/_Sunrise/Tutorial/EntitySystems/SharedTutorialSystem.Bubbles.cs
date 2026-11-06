using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems;

public abstract partial class SharedTutorialSystem
{
    private void UpdateTutorialBubble(Entity<TutorialPlayerComponent> ent, TutorialStepPrototype step)
    {
        var targetEntity = step.Target != null
            ? TryFindNearestTargetEntity(ent, step.Target, step)
            : null;

        ent.Comp.Target = targetEntity;

        if (step.Bubble == null)
        {
            Dirty(ent, ent.Comp);
            return;
        }

        var bubbleTarget = step.Bubble.AttachToTarget ? targetEntity : ent;

        if (bubbleTarget == null || !Exists(bubbleTarget.Value))
        {
            ClearTutorialBubble(ent);
            Dirty(ent, ent.Comp);
            return;
        }

        if (ent.Comp.CurrentBubbleTarget is { } previous && previous != bubbleTarget.Value && Exists(previous))
            RemComp<TutorialBubbleComponent>(previous);

        var bubble = EnsureComp<TutorialBubbleComponent>(bubbleTarget.Value);
        bubble.Instruction = step.Bubble.Text;
        Dirty(bubbleTarget.Value, bubble);

        ent.Comp.CurrentBubbleTarget = bubbleTarget;
        Dirty(ent, ent.Comp);
    }

    private void ClearTutorialBubble(Entity<TutorialPlayerComponent> ent)
    {
        if (ent.Comp.CurrentBubbleTarget is { } oldTarget && Exists(oldTarget))
            RemComp<TutorialBubbleComponent>(oldTarget);

        ent.Comp.CurrentBubbleTarget = null;
    }

    private EntityUid? TryFindNearestTargetEntity(EntityUid uid, EntProtoId? target, TutorialStepPrototype proto)
    {
        var origin = _transform.GetMapCoordinates(uid);
        var best = EntityUid.Invalid;
        var bestDistSq = float.MaxValue;

        foreach (var ent in _lookupSystem.GetEntitiesInRange(uid, proto.ObserveRange))
        {
            var meta = MetaData(ent);
            if (meta.EntityPrototype?.ID != target)
                continue;

            var distSq = (_transform.GetMapCoordinates(ent).Position - origin.Position).LengthSquared();
            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            best = ent;
        }

        return best;
    }
}
