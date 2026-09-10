using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Objectives.Conditions;

/// <summary>
/// Records vending machine eject actions and supports matching the dispensed item prototype.
/// </summary>
public sealed partial class VendingMachineTakeObjectiveConditionSystem
    : ObjectiveEventConditionSystem<VendingMachineTakeObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<ObjectiveInteractionObserverComponent>(VendingMachineUiKey.Key, subs =>
        {
            subs.Event<VendingMachineEjectMessage>(OnVendingEject);
        });
    }

    private void OnVendingEject(Entity<ObjectiveInteractionObserverComponent> ent, ref VendingMachineEjectMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
            return;

        RecordObservedEvent(ent, DefaultKey, actor);
        RecordObservedEvent(ent, VendingMachineTakeObjectiveCondition.GetItemKey(new EntProtoId(args.ID)), actor);
    }

    protected override bool ShouldObserve(VendingMachineTakeObjectiveCondition condition, EntityUid target)
    {
        return condition.ItemTarget != null
            ? HasComp<VendingMachineComponent>(target)
            : base.ShouldObserve(condition, target);
    }

    protected override bool MatchesEventTarget(
        VendingMachineTakeObjectiveCondition condition,
        EntityUid? primary,
        EntityUid? secondary)
    {
        return condition.ItemTarget != null || base.MatchesEventTarget(condition, primary, secondary);
    }
}
