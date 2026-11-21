using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared._Sunrise.Objectives.Conditions;
using Content.Shared.Storage.Components;

namespace Content.Server._Sunrise.Objectives.Conditions;

/// <summary>
/// Records successful physical storage openings on observed entities.
/// </summary>
public sealed partial class StorageOpenObjectiveConditionSystem
    : ObjectiveEventConditionSystem<StorageOpenObjectiveCondition, ObjectiveContainerOwnerComponent, ObjectiveContainerObserverComponent>
{
    // Шкафы, ящики и подобные хранилища открываются физически через EntityStorageComponent.
    [SubscribeLocalEvent]
    private void OnEntityStorageOpen(Entity<ObjectiveContainerObserverComponent> ent, ref StorageOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        RecordObservedEvent(ent, DefaultKey, args.User);
    }
}
