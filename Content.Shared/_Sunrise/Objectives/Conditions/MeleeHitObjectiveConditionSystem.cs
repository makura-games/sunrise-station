using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Отслеживает обычные и размашистые попадания владельца цели.
/// </summary>
public sealed partial class MeleeHitObjectiveConditionSystem
    : ObjectiveEventConditionSystem<MeleeHitObjectiveCondition, ObjectiveCombatOwnerComponent, ObjectiveCombatObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveCombatObserverComponent, MeleeHitEvent>(OnObservedWeaponHit);
        SubscribeLocalEvent<ObjectiveCombatOwnerComponent, MeleeHitEvent>(OnUnarmedHit);
    }

    private void OnObservedWeaponHit(Entity<ObjectiveCombatObserverComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Owner == args.User || !ent.Comp.Registrations.ContainsValue(args.User))
            return;

        RecordHits(ent, args);
    }

    private void OnUnarmedHit(Entity<ObjectiveCombatOwnerComponent> ent, ref MeleeHitEvent args)
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
            if (!TryGetPrototypeId(args.HitEntities[i], out var hitTarget))
                continue;

            var key = MeleeHitObjectiveCondition.GetCounterKey(hitTarget, args.Direction != null);
            RecordEvent(args.User, key, weapon);
        }
    }
}

/// <summary>
/// Проверяет попадание заданным оружием и различает обычную и размашистую атаку.
/// </summary>
public sealed partial class MeleeHitObjectiveCondition
    : ObjectiveEventConditionBase<MeleeHitObjectiveCondition>
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
        return $"{nameof(MeleeHitObjectiveCondition)}:{hitTarget.Id}:{wide}";
    }
}
