using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Records melee attacks performed by an objective owner against observed entities.
/// </summary>
public sealed partial class AttackObjectiveConditionSystem : ObjectiveEventConditionSystem<AttackObjectiveCondition, ObjectiveCombatOwnerComponent, ObjectiveCombatObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveCombatObserverComponent, AttackedEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<ObjectiveCombatObserverComponent> ent, ref AttackedEvent args)
    {
        RecordObservedEvent(ent, DefaultKey, args.User, secondaryTarget: args.Used);

        if (TryGetPrototypeId(args.Used, out var usedPrototype))
            RecordObservedEvent(ent, AttackObjectiveCondition.GetUsedKey(usedPrototype), args.User);
    }
}

/// <summary>
/// Checks if the player has attacked a target entity.
/// </summary>
public sealed partial class AttackObjectiveCondition : ObjectiveEventConditionBase<AttackObjectiveCondition>
{
    /// <summary>
    /// Необязательный прототип сущности, которой должен быть нанесён удар.
    /// Для безоружной атаки это прототип самого игрока.
    /// </summary>
    [DataField]
    public EntProtoId? Used;

    public override string CounterKey => Used is { } used
        ? GetUsedKey(used)
        : base.CounterKey;

    public override bool ObserveAnyWithoutTarget => true;

    public static string GetUsedKey(EntProtoId used)
    {
        return string.Concat(nameof(AttackObjectiveCondition), ".Used.", used.Id);
    }
}
