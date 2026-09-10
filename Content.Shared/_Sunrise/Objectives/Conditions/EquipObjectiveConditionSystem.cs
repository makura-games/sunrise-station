using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records equipment events and optionally separates them by inventory slot.
/// </summary>
public sealed partial class EquipObjectiveConditionSystem : ObjectiveEventConditionSystem<EquipObjectiveCondition, ObjectiveInteractionOwnerComponent, ObjectiveInteractionObserverComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveInteractionOwnerComponent, DidEquipEvent>(OnDidEquip);
    }

    private void OnDidEquip(Entity<ObjectiveInteractionOwnerComponent> ent, ref DidEquipEvent args)
    {
        if (_timing.ApplyingState)
            return;

        RecordEvent(ent, DefaultKey, args.Equipment);
        RecordEvent(ent, EquipObjectiveCondition.GetSlotKey(args.SlotFlags), args.Equipment);
    }
}

/// <summary>
/// Checks if the player has equipped a target entity.
/// </summary>
public sealed partial class EquipObjectiveCondition : ObjectiveEventConditionBase<EquipObjectiveCondition>
{
    /// <summary>
    /// Optional slot mask that the equipment event must match.
    /// </summary>
    [DataField]
    public SlotFlags? Slot;

    public override string CounterKey => Slot == null
        ? base.CounterKey
        : string.Concat(base.CounterKey, ":", Slot.Value);

    /// <summary>
    /// Builds the counter key used for slot-specific equipment checks.
    /// </summary>
    public static string GetSlotKey(SlotFlags slot)
    {
        return string.Concat(nameof(EquipObjectiveCondition), ":", slot);
    }
}
