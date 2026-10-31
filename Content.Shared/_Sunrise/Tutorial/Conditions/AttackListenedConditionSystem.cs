using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Records melee attacks performed by a tutorial player against observed entities.
/// </summary>
public sealed partial class AttackListenedConditionSystem : EventListenedConditionSystemBase<AttackListenedCondition>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialObservableComponent, AttackedEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<TutorialObservableComponent> ent, ref AttackedEvent args)
    {
        if (!ent.Comp.Observers.Contains(args.User))
            return;

        RecordEvent(args.User, DefaultKey, ent, args.Used);

        if (Tutorial.TryGetPrototypeId(args.Used, out var usedPrototype))
            RecordEvent(args.User, AttackListenedCondition.GetUsedKey(usedPrototype), ent);
    }
}

/// <summary>
/// Checks if the player has attacked a target entity.
/// </summary>
public sealed partial class AttackListenedCondition : EventListenedConditionBase<AttackListenedCondition>
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
        return string.Concat(nameof(AttackListenedCondition), ".Used.", used.Id);
    }
}
