using Content.Server._Sunrise.CryoTeleport;
using Content.Shared._Sunrise.Mech;
using Content.Shared.Coordinates;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Mech;

/// <inheritdoc/>
public sealed partial class SunriseMechSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechVulnerableToEMPComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<VehicleOperatorComponent, BeforeCryoTeleportEvent>(OnCryoTeleportAttemptEvent);
    }

    private void OnCryoTeleportAttemptEvent(Entity<VehicleOperatorComponent> ent, ref BeforeCryoTeleportEvent args)
    {
        if (ent.Comp.Vehicle is not { } mech || !HasComp<MechComponent>(mech))
            return;

        _vehicle.TryExit(mech);
    }

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
