using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Отслеживает успешные попытки обезоруживания владельца цели.
/// </summary>
public sealed partial class DisarmObjectiveConditionSystem
    : ObjectiveEventConditionSystem<DisarmObjectiveCondition, ObjectiveCombatOwnerComponent, ObjectiveCombatObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveCombatObserverComponent, DisarmedEvent>(
            OnDisarmed,
            after: [typeof(SharedStaminaSystem)]);
    }

    private void OnDisarmed(Entity<ObjectiveCombatObserverComponent> ent, ref DisarmedEvent args)
    {
        if (!args.Handled)
            return;

        RecordObservedEvent(ent, DefaultKey, args.Source);
    }
}

/// <summary>
/// Проверяет, что игрок успешно применил обезоруживание к указанной сущности.
/// </summary>
public sealed partial class DisarmObjectiveCondition : ObjectiveEventConditionBase<DisarmObjectiveCondition>;
