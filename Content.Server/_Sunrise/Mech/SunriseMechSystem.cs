using Content.Server._Sunrise.CryoTeleport;
using Content.Shared._Sunrise.Mech;
using Content.Shared.Coordinates;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Mech;

/// <inheritdoc/>
public sealed partial class SunriseMechSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMechSystem _mech = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechVulnerableToEMPComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<MechPilotComponent, BeforeCryoTeleportEvent>(OnCryoTeleportAttemptEvent);
    }

    // Sunrise-start
    private void OnCryoTeleportAttemptEvent(EntityUid uid, MechPilotComponent component, BeforeCryoTeleportEvent args)
    {
        if (!TryComp<MechComponent>(component.Mech, out var mechComponent))
            return;
        _mech.TryEject(uid, mechComponent);
    }
    // Sunrise-end

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MechVulnerableToEMPComponent, MechOnEMPPulseComponent>();
        while (query.MoveNext(out var uid, out var comp, out var emp))
        {
            var curTime = _timing.CurTime;

            if (emp.NextEffectTime > curTime)
                continue;

            emp.NextEffectTime = curTime + emp.EffectInterval;

            SpawnAttachedTo(comp.EffectEMP, uid.ToCoordinates());

            if (curTime > comp.NextPulseTime)
                RemComp<MechOnEMPPulseComponent>(uid);
        }
    }

    private void OnEmpPulse(Entity<MechVulnerableToEMPComponent> ent, ref EmpPulseEvent args)
    {
        var curTime = _timing.CurTime;

        if (curTime < ent.Comp.NextPulseTime)
            return;

        ent.Comp.NextPulseTime = curTime + ent.Comp.CooldownTime;

        _damageable.TryChangeDamage(ent.Owner, ent.Comp.EmpDamage);

        EnsureComp<MechOnEMPPulseComponent>(ent);
    }
}
