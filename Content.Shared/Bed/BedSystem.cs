using Content.Shared._Sunrise.Bed;
using Content.Shared.Actions;
using Content.Shared.Bed.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Events;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.Metabolism;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Shared.Bed;


public sealed partial class BedSystem : EntitySystem
{
    [Dependency] private ActionContainerSystem _actConts = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MetabolizerSystem _metabolizer = default!;
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private SleepingSystem _sleepingSystem = default!;

    [Dependency] private EntityQuery<SleepingComponent> _sleepingQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Sunrise-Start
        SubscribeLocalEvent<CanSleepOnBuckleComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<CanSleepOnBuckleComponent, StrappedEvent>(OnStrapped);
        // Sunrise-End

        // Sunrise-Edit
        //SubscribeLocalEvent<HealOnBuckleComponent, MapInitEvent>(OnHealMapInit);
        SubscribeLocalEvent<HealOnBuckleComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<HealOnBuckleComponent, UnstrappedEvent>(OnUnstrapped);

        SubscribeLocalEvent<StasisBedComponent, StrappedEvent>(OnStasisStrapped);
        SubscribeLocalEvent<StasisBedComponent, UnstrappedEvent>(OnStasisUnstrapped);
        SubscribeLocalEvent<StasisBedComponent, GotEmaggedEvent>(OnStasisEmagged);
        SubscribeLocalEvent<StasisBedComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<StasisBedBuckledComponent, GetMetabolicMultiplierEvent>(OnStasisGetMetabolicMultiplier);
    }

    // Sunrise-Start
    private void OnStrapped(Entity<CanSleepOnBuckleComponent> bed, ref StrappedEvent args)
    {
        var canSleep = EnsureComp<CanSleepComponent>(args.Buckle);
        _actionsSystem.AddAction(args.Buckle.Owner, ref canSleep.SleepAction, SleepingSystem.SleepActionId, args.Buckle.Owner);
    }

    private void OnUnstrapped(Entity<CanSleepOnBuckleComponent> bed, ref UnstrappedEvent args)
    {
        if (!TryComp<CanSleepComponent>(args.Buckle.Owner, out var canSleep))
            return;

        RemComp<CanSleepComponent>(args.Buckle.Owner);
        _actionsSystem.RemoveAction(args.Buckle.Owner, canSleep.SleepAction);
        _sleepingSystem.TryWaking(args.Buckle.Owner);
        if (canSleep.SleepAction != null)
            _actConts.RemoveAction(canSleep.SleepAction.Value);
    }
    // Sunrise-End

    // Sunrise-Edit
    // private void OnHealMapInit(Entity<HealOnBuckleComponent> ent, ref MapInitEvent args)
    // {
    //     _actConts.EnsureAction(ent.Owner, ref ent.Comp.SleepAction, SleepingSystem.SleepActionId);
    //     Dirty(ent);
    // }

    private void OnStrapped(Entity<HealOnBuckleComponent> bed, ref StrappedEvent args)
    {
        EnsureComp<HealOnBuckleHealingComponent>(bed);
        bed.Comp.NextHealTime = _timing.CurTime + TimeSpan.FromSeconds(bed.Comp.HealTime);
    }

    private void OnUnstrapped(Entity<HealOnBuckleComponent> bed, ref UnstrappedEvent args)
    {
        // Sunrise-Edit
        // If the entity being unbuckled is terminating, we shouldn't try to act upon it, as some components may be gone
        //if (!Terminating(args.Buckle.Owner))
        //{
        //    _actionsSystem.RemoveAction(args.Buckle.Owner, bed.Comp.SleepAction);
        //    _sleepingSystem.TryWaking(args.Buckle.Owner);
        //}
        RemComp<HealOnBuckleHealingComponent>(bed);
    }

    private void OnStasisStrapped(Entity<StasisBedComponent> ent, ref StrappedEvent args)
    {
        EnsureComp<StasisBedBuckledComponent>(args.Buckle);
        _metabolizer.UpdateMetabolicMultiplier(args.Buckle);
    }

    private void OnStasisUnstrapped(Entity<StasisBedComponent> ent, ref UnstrappedEvent args)
    {
        RemComp<StasisBedBuckledComponent>(ent);
        _metabolizer.UpdateMetabolicMultiplier(args.Buckle);
    }

    private void OnStasisEmagged(Entity<StasisBedComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(ent, EmagType.Interaction))
            return;

        ent.Comp.Multiplier = 1f / ent.Comp.Multiplier;
        UpdateMetabolisms(ent.Owner);
        Dirty(ent);

        args.Handled = true;
    }

    private void OnPowerChanged(Entity<StasisBedComponent> ent, ref PowerChangedEvent args)
    {
        UpdateMetabolisms(ent.Owner);
    }

    private void OnStasisGetMetabolicMultiplier(Entity<StasisBedBuckledComponent> ent, ref GetMetabolicMultiplierEvent args)
    {
        if (!TryComp<BuckleComponent>(ent, out var buckle) || buckle.BuckledTo is not { } buckledTo)
            return;

        if (!TryComp<StasisBedComponent>(buckledTo, out var stasis))
            return;

        if (!_powerReceiver.IsPowered(buckledTo))
            return;

        args.Multiplier *= stasis.Multiplier;
    }

    private void UpdateMetabolisms(Entity<StrapComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        foreach (var buckledEntity in ent.Comp.BuckledEntities)
        {
            _metabolizer.UpdateMetabolicMultiplier(buckledEntity);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Sunrise-start Добавлен TransformComponent для проверки на якорность кровати
        var query = EntityQueryEnumerator<HealOnBuckleHealingComponent, HealOnBuckleComponent, StrapComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var bedComponent, out var strapComponent, out var xform)) // Добавлено xform
        // Sunrise-end
        {
            // Sunrise-Start
            if (!xform.Anchored)
                continue;
            // Sunrise-End

            if (_timing.CurTime < bedComponent.NextHealTime)
                continue;

            bedComponent.NextHealTime += TimeSpan.FromSeconds(bedComponent.HealTime);

            Dirty(uid, bedComponent);

            if (strapComponent.BuckledEntities.Count == 0)
                continue;

            foreach (var healedEntity in strapComponent.BuckledEntities)
            {
                if (_mobStateSystem.IsDead(healedEntity))
                    continue;

                var damage = bedComponent.Damage;

                if (_sleepingQuery.HasComp(healedEntity))
                    damage *= bedComponent.SleepMultiplier;

                damage *= GetSunriseHealingMultiplier(healedEntity); // Sunrise-edit уменьшаем хил если одет скафандр и т.д борьба с кроватью в космосе.
                _damageableSystem.TryChangeDamage(healedEntity, damage, true, origin: uid);
            }
        }
    }
}
