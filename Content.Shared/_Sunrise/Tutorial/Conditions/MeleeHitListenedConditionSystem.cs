using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Отслеживает обычные и размашистые попадания игрока туториала.
/// </summary>
public sealed partial class MeleeHitListenedConditionSystem
    : EventListenedConditionSystemBase<MeleeHitListenedCondition>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialObservableComponent, MeleeHitEvent>(OnObservedWeaponHit);
        SubscribeLocalEvent<TutorialPlayerComponent, MeleeHitEvent>(OnUnarmedHit);
    }

    private void OnObservedWeaponHit(Entity<TutorialObservableComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Owner == args.User || !ent.Comp.Observers.Contains(args.User))
            return;

        RecordHits(ent, args);
    }

    private void OnUnarmedHit(Entity<TutorialPlayerComponent> ent, ref MeleeHitEvent args)
    {
        if (args.User != ent.Owner || args.Weapon != ent.Owner)
            return;

        RecordHits(ent, args);
    }

    private void RecordHits(EntityUid weapon, MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        for (var i = 0; i < args.HitEntities.Count; i++)
        {
            if (!Tutorial.TryGetPrototypeId(args.HitEntities[i], out var hitTarget))
                continue;

            var key = MeleeHitListenedCondition.GetCounterKey(hitTarget, args.Direction != null);
            RecordEvent(args.User, key, weapon);
        }
    }
}

/// <summary>
/// Проверяет попадание заданным оружием и различает обычную и размашистую атаку.
/// </summary>
public sealed partial class MeleeHitListenedCondition
    : EventListenedConditionBase<MeleeHitListenedCondition>
{
    /// <summary>
    /// Прототип сущности, по которой нужно попасть.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId HitTarget;

    /// <summary>
    /// Должен ли засчитываться размашистый, а не обычный удар.
    /// </summary>
    [DataField]
    public bool Wide;

    public override string CounterKey => GetCounterKey(HitTarget, Wide);

    public static string GetCounterKey(EntProtoId hitTarget, bool wide)
    {
        return $"{nameof(MeleeHitListenedCondition)}:{hitTarget.Id}:{wide}";
    }
}
