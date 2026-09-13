using Content.Server._Sunrise.Heartbeat.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;

namespace Content.Server._Sunrise.Heartbeat.Systems;

public sealed partial class HeartbeatSystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    // Минимальное и максимальное время между ударами сердца
    private const float MinimumCooldown = 0.5f;
    private const float MaximumCooldown = 3f;

    private void OnMobStateChanged(Entity<CritHeartbeatComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical)
        {
            RemComp<ActiveHeartbeatComponent>(ent);
            return;
        }

        var activeHeartbeat = EnsureComp<ActiveHeartbeatComponent>(ent);

        TryCalculateCurrentState((ent.Owner, activeHeartbeat));
        SetNextTime(activeHeartbeat);
    }

    /// <summary>
    /// Подтягивает значения эффектов в зависимости от того, насколько игрок продамажен
    /// Чем выше урон -> тем медленнее бьется сердце и тем более глухой звук
    /// </summary>
    private void OnDamage(Entity<ActiveHeartbeatComponent> ent, ref DamageChangedEvent args)
    {
        TryCalculateCurrentState(ent, args.Damageable);
    }

    /// <summary>
    /// Подсчитывает нужные данные о текущем уроне тела и в зависимости от них задает нужный pitch и cooldown для сердцебиения
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="damageable"></param>
    /// <returns></returns>
    private bool TryCalculateCurrentState(Entity<ActiveHeartbeatComponent> ent, DamageableComponent? damageable = null)
    {
        if (!Resolve(ent.Owner, ref damageable))
            return false;

        var totalDamage = _damageable.GetTotalDamage((ent.Owner, damageable)).Float();

        var pitch = Math.Min(1f, 100f / totalDamage);

        var excess = Math.Max(0f, totalDamage - 100f);
        var cooldownSeconds = MinimumCooldown + (excess / 100f) * (MaximumCooldown - MinimumCooldown);

        ent.Comp.Pitch = pitch;
        ent.Comp.NextHeartbeatCooldown = TimeSpan.FromSeconds(cooldownSeconds);

        return true;
    }

}
