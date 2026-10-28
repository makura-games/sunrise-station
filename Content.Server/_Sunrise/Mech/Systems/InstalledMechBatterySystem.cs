using Content.Server._Sunrise.Mech.Components;
using Content.Server.Mech.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.FixedPoint;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Mech.Systems;

/// <summary>
/// Links an installed battery to its mech and synchronizes the derived charge state.
/// </summary>
public sealed partial class InstalledMechBatterySystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private MechSystem _mech = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityQuery<MechComponent> _mechQuery = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechComponent, EntRemovedFromContainerMessage>(OnBatteryRemoved);
        SubscribeLocalEvent<InstalledMechBatteryComponent, ChargeChangedEvent>(OnChargeChanged);

    }

    private void OnBatteryRemoved(Entity<MechComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container != ent.Comp.BatterySlot)
            return;

        if (TryComp<InstalledMechBatteryComponent>(args.Entity, out var installed) && installed.Mech == ent.Owner)
            RemComp<InstalledMechBatteryComponent>(args.Entity);

        SetMechEnergy(ent, 0f, 0f);
        _movementSpeedModifier.RefreshMovementSpeedModifiers(ent);
    }

    private void OnChargeChanged(Entity<InstalledMechBatteryComponent> ent, ref ChargeChangedEvent args)
    {
        if (!_mechQuery.TryComp(ent.Comp.Mech, out var mech))
            return;

        if (mech.BatterySlot.ContainedEntity != ent.Owner)
            return;

        SetMechEnergy((ent.Comp.Mech, mech), args.CurrentCharge, args.MaxCharge);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<InstalledMechBatteryComponent, BatteryComponent>();
        while (query.MoveNext(out var uid, out var installed, out var battery))
        {
            if (installed.NextUpdate > curTime)
                continue;

            installed.NextUpdate = curTime + UpdateInterval;

            if (!_mechQuery.TryComp(installed.Mech, out var mech) || mech.BatterySlot.ContainedEntity != uid)
            {
                RemCompDeferred<InstalledMechBatteryComponent>(uid);
                continue;
            }

            SynchronizeEnergy((installed.Mech, mech), (uid, battery));
        }
    }

    /// <summary>
    /// Links an installed battery to its mech and refreshes dependent state.
    /// </summary>
    public void AttachBattery(Entity<MechComponent?> mech, Entity<BatteryComponent?> battery)
    {
        if (!Resolve(mech, ref mech.Comp, false) || !Resolve(battery, ref battery.Comp, false))
            return;

        var installed = EnsureComp<InstalledMechBatteryComponent>(battery);
        installed.Mech = mech.Owner;
        installed.NextUpdate = _timing.CurTime + UpdateInterval;

        SynchronizeEnergy((mech.Owner, mech.Comp), (battery.Owner, battery.Comp));
        _movementSpeedModifier.RefreshMovementSpeedModifiers(mech.Owner);
    }

    private void SynchronizeEnergy(Entity<MechComponent> mech, Entity<BatteryComponent> battery)
    {
        SetMechEnergy(mech, _battery.GetCharge(battery.AsNullable()), battery.Comp.MaxCharge);
    }

    private void SetMechEnergy(Entity<MechComponent> mech, float charge, float maxCharge)
    {
        var energy = FixedPoint2.New(charge);
        var maxEnergy = FixedPoint2.New(maxCharge);
        if (mech.Comp.Energy == energy && mech.Comp.MaxEnergy == maxEnergy)
            return;

        mech.Comp.Energy = energy;
        mech.Comp.MaxEnergy = maxEnergy;

        Dirty(mech);
        _actionBlocker.UpdateCanMove(mech);
        _mech.UpdateUserInterface(mech, mech.Comp);
    }
}
