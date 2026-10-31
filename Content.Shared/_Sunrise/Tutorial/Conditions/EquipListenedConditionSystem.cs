using System.Collections.Generic;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Components.Trackers;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Records equipment events and optionally separates them by inventory slot.
/// </summary>
public sealed partial class EquipListenedConditionSystem : EventListenedConditionSystemBase<EquipListenedCondition>
{
    [Dependency] private IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialPlayerComponent, DidEquipEvent>(OnDidEquip);
    }

    private void OnDidEquip(Entity<TutorialPlayerComponent> ent, ref DidEquipEvent args)
    {
        if (_timing.ApplyingState)
            return;

        RecordEvent(ent, DefaultKey, args.Equipment);
        RecordEvent(ent, EquipListenedCondition.GetSlotKey(args.SlotFlags), args.Equipment);

        if (!TryComp<TutorialTrackerComponent>(ent, out var tracker))
            return;

        Tutorial.TryObserveEntity(ent, args.Equipment, tracker);
    }
}

/// <summary>
/// Checks if the player has equipped a target entity.
/// </summary>
public sealed partial class EquipListenedCondition : EventListenedConditionBase<EquipListenedCondition>
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
        return string.Concat(nameof(EquipListenedCondition), ":", slot);
    }
}
