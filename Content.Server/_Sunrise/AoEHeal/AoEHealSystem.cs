using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.AoEHeal;

/// <summary>
/// Лечение по области
/// </summary>
public sealed partial class AoEHealSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookupSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;

    private TimeSpan? _prev = TimeSpan.Zero;
    private readonly TimeSpan _delay = TimeSpan.FromSeconds(2);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime - _prev < _delay)
            return;
        _prev = _timing.CurTime;

        var query = EntityQueryEnumerator<AoEHealComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var aoEHealComponent, out var xform))
        {
            var targetsQuery =
                _lookupSystem.GetEntitiesInRange<DamageableComponent>(xform.Coordinates, aoEHealComponent.Range);
            foreach (var target in targetsQuery
                         // Проходит ли ВЛ?
                         .Where(target => !_whitelist.IsWhitelistFail(aoEHealComponent.EntityWhitelist, target))
                         // Если нам важно, то жива ли цель?
                         .Where(target => !aoEHealComponent.AliveTargets || _mobState.IsAlive(target)))
            {
                if (!aoEHealComponent.HealSelf && target.Owner == uid)
                    continue;

                if (aoEHealComponent.Threshold != null && // AoE компоненту важно хилить до какого-то уровня от макс здоровья
                    _mobThreshold.TryGetDeadThreshold(target, out var threshold) &&
                    _damageableSystem.GetTotalDamage(target.AsNullable()) < threshold * (1f - aoEHealComponent.Threshold)) // Не лечим если урона мало
                    continue;

                _damageableSystem.TryChangeDamage(target.Owner, aoEHealComponent.Damage);
            }
        }
    }
}
