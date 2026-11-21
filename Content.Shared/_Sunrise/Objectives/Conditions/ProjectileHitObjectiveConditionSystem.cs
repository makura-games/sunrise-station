using Content.Shared._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Objectives;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives.Conditions;

/// <summary>
/// Записывает попадания снарядов, выпущенных владельцем цели.
/// </summary>
public sealed partial class ProjectileHitObjectiveConditionSystem
    : ObjectiveEventConditionSystem<ProjectileHitObjectiveCondition, ObjectiveCombatOwnerComponent, ObjectiveCombatObserverComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ObjectiveProjectileSourceComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<ObjectiveCombatObserverComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnAmmoShot(Entity<ObjectiveProjectileSourceComponent> ent, ref AmmoShotEvent args)
    {
        if (!TryComp<ObjectiveCombatObserverComponent>(ent, out var sourceObservable) ||
            sourceObservable.Registrations.Count == 0)
        {
            return;
        }

        for (var i = 0; i < args.FiredProjectiles.Count; i++)
        {
            var projectile = args.FiredProjectiles[i];
            if (TerminatingOrDeleted(projectile))
                continue;

            CopyObserverRegistrations((ent.Owner, sourceObservable), projectile);
        }
    }

    private void OnProjectileHit(Entity<ObjectiveCombatObserverComponent> projectile, ref ProjectileHitEvent args)
    {
        if (args.Shooter is not { } shooter)
            return;

        if (!TryComp<ProjectileComponent>(projectile, out var projectileComponent))
            return;

        if (!TryGetPrototypeId(args.Target, out var hitTarget))
            return;

        RecordObservedEvent(
            projectile,
            ProjectileHitObjectiveCondition.GetHitTargetKey(hitTarget),
            shooter,
            projectileComponent.Weapon);
    }

    protected override void OnObserverRegistrationAdded(
        Entity<ObjectiveCombatObserverComponent> observer,
        ObjectiveConditionHandle handle)
    {
        if (Objectives.TryGetCondition<ProjectileHitObjectiveCondition>(handle, out _))
            EnsureComp<ObjectiveProjectileSourceComponent>(observer);
    }

    protected override void OnObserverRegistrationRemoved(
        Entity<ObjectiveCombatObserverComponent> observer,
        ObjectiveConditionHandle handle)
    {
        foreach (var registration in observer.Comp.Registrations.Keys)
        {
            if (Objectives.TryGetCondition<ProjectileHitObjectiveCondition>(registration, out _))
                return;
        }

        RemComp<ObjectiveProjectileSourceComponent>(observer);
    }
}

/// <summary>
/// Проверяет попадания по цели из указанного оружия.
/// </summary>
public sealed partial class ProjectileHitObjectiveCondition
    : ObjectiveEventConditionBase<ProjectileHitObjectiveCondition>
{
    /// <summary>
    /// Прототип сущности, в которую должен попасть снаряд.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId HitTarget;

    public override string CounterKey => GetHitTargetKey(HitTarget);

    public static string GetHitTargetKey(EntProtoId hitTarget)
    {
        return string.Concat(nameof(ProjectileHitObjectiveCondition), ".HitTarget.", hitTarget.Id);
    }
}
