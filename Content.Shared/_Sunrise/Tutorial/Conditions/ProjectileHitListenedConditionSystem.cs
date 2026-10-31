using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Tutorial.Conditions;

/// <summary>
/// Записывает попадания снарядов, выпущенных игроком туториала.
/// </summary>
public sealed partial class ProjectileHitListenedConditionSystem
    : EventListenedConditionSystemBase<ProjectileHitListenedCondition>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialProjectileSourceComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<TutorialObservableComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnAmmoShot(Entity<TutorialProjectileSourceComponent> ent, ref AmmoShotEvent args)
    {
        if (!TryComp<TutorialObservableComponent>(ent, out var sourceObservable) ||
            sourceObservable.Observers.Count == 0)
        {
            return;
        }

        for (var i = 0; i < args.FiredProjectiles.Count; i++)
        {
            var projectile = args.FiredProjectiles[i];
            if (TerminatingOrDeleted(projectile))
                continue;

            var observable = EnsureComp<TutorialObservableComponent>(projectile);
            observable.Observers.UnionWith(sourceObservable.Observers);
            Dirty(projectile, observable);
        }
    }

    private void OnProjectileHit(Entity<TutorialObservableComponent> projectile, ref ProjectileHitEvent args)
    {
        if (args.Shooter is not { } shooter)
            return;

        if (!projectile.Comp.Observers.Contains(shooter))
            return;

        if (!TryComp<ProjectileComponent>(projectile, out var projectileComponent))
            return;

        if (!Tutorial.TryGetPrototypeId(args.Target, out var hitTarget))
            return;

        RecordEvent(
            shooter,
            ProjectileHitListenedCondition.GetHitTargetKey(hitTarget),
            projectileComponent.Weapon);
    }
}

/// <summary>
/// Проверяет попадания по цели из указанного оружия.
/// </summary>
public sealed partial class ProjectileHitListenedCondition
    : EventListenedConditionBase<ProjectileHitListenedCondition>
{
    /// <summary>
    /// Прототип сущности, в которую должен попасть снаряд.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId HitTarget;

    public override string CounterKey => GetHitTargetKey(HitTarget);

    public static string GetHitTargetKey(EntProtoId hitTarget)
    {
        return string.Concat(nameof(ProjectileHitListenedCondition), ".HitTarget.", hitTarget.Id);
    }
}
