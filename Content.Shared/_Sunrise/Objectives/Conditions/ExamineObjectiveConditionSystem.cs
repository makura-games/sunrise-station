using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records examine events performed by objective owners on observed entities.
/// </summary>
public sealed partial class ExamineObjectiveConditionSystem : ObjectiveEventConditionSystem<ExamineObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionObserverComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<ObjectiveInteractionObserverComponent> ent, ref ExaminedEvent args)
    {
        RecordObservedEvent(ent, DefaultKey, args.Examiner, args.Examined);
    }
}

/// <summary>
/// Checks if the player has examined a target entity.
/// </summary>
public sealed partial class ExamineObjectiveCondition : ObjectiveEventConditionBase<ExamineObjectiveCondition>
{
    public override bool ObserveAnyWithoutTarget => true;
}

