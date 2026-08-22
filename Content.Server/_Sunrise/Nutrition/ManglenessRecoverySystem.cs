using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Nutrition;

public sealed class ManglenessRecoverySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HungerComponent, ComponentInit>(OnHungerInit);
        SubscribeLocalEvent<ThirstComponent, ComponentInit>(OnThirstInit);
        SubscribeLocalEvent<DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<HungerComponent, HungerDecayRateModifierEvent>(OnHungerDecayRateModifier);
        SubscribeLocalEvent<ThirstComponent, ThirstDecayRateModifierEvent>(OnThirstDecayRateModifier);
    }

    private void OnHungerInit(EntityUid uid, HungerComponent component, ComponentInit args)
    {
        component.HadMangleness = _damageable.HasMangleness(uid);
    }

    private void OnThirstInit(EntityUid uid, ThirstComponent component, ComponentInit args)
    {
        component.HadMangleness = _damageable.HasMangleness(uid);
    }

    private void OnDamageChanged(DamageChangedEvent args)
    {
        var uid = args.Damageable.Owner;
        var hasMangleness = _damageable.HasMangleness(uid);

        if (TryComp<HungerComponent>(uid, out var hunger) && hunger.HadMangleness != hasMangleness)
        {
            hunger.HadMangleness = hasMangleness;
            DirtyField(uid, hunger, nameof(HungerComponent.HadMangleness));
            RaiseLocalEvent(uid, new HungerManglenessChangedEvent(hasMangleness));
        }

        if (TryComp<ThirstComponent>(uid, out var thirst) && thirst.HadMangleness != hasMangleness)
        {
            thirst.HadMangleness = hasMangleness;
            DirtyField(uid, thirst, nameof(ThirstComponent.HadMangleness));
            RaiseLocalEvent(uid, new ThirstManglenessChangedEvent(hasMangleness));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var hungerQuery = EntityQueryEnumerator<HungerComponent>();
        while (hungerQuery.MoveNext(out var uid, out var hunger))
        {
            if (_timing.CurTime < hunger.NextThresholdUpdateTime || _mobState.IsDead(uid))
                continue;

            if (!hunger.HadMangleness)
                continue;

            if (hunger.CurrentThreshold >= HungerThreshold.Okay)
            {
                var heal = new DamageSpecifier();
                heal.DamageDict.Add("Mangleness", hunger.ManglenessHealingOkay);
                _damageable.TryChangeDamage(uid, heal, true, false);
            }
            else if (hunger.CurrentThreshold == HungerThreshold.Peckish)
            {
                var heal = new DamageSpecifier();
                heal.DamageDict.Add("Mangleness", hunger.ManglenessHealingPeckish);
                _damageable.TryChangeDamage(uid, heal, true, false);
            }
        }

        var thirstQuery = EntityQueryEnumerator<ThirstComponent>();
        while (thirstQuery.MoveNext(out var uid, out var thirst))
        {
            if (_timing.CurTime < thirst.NextUpdateTime || _mobState.IsDead(uid))
                continue;

            if (!thirst.HadMangleness)
                continue;

            if (thirst.CurrentThirstThreshold >= ThirstThreshold.Okay)
            {
                var heal = new DamageSpecifier();
                heal.DamageDict.Add("Mangleness", thirst.ManglenessHealingOkay);
                _damageable.TryChangeDamage(uid, heal, true, false);
            }
            else if (thirst.CurrentThirstThreshold == ThirstThreshold.Thirsty)
            {
                var heal = new DamageSpecifier();
                heal.DamageDict.Add("Mangleness", thirst.ManglenessHealingThirsty);
                _damageable.TryChangeDamage(uid, heal, true, false);
            }
        }
    }

    private void OnHungerDecayRateModifier(EntityUid uid, HungerComponent component, HungerDecayRateModifierEvent args)
    {
        if (!component.HadMangleness)
            return;

        switch (component.CurrentThreshold)
        {
            case HungerThreshold.Peckish:
                args.ActualDecayRate *= component.ManglenessDecayMultPeckish;
                break;
            case HungerThreshold.Okay:
                args.ActualDecayRate *= component.ManglenessDecayMultOkay;
                break;
            case HungerThreshold.Overfed:
                args.ActualDecayRate *= component.ManglenessDecayMultOverfed;
                break;
        }
    }

    private void OnThirstDecayRateModifier(EntityUid uid, ThirstComponent component, ThirstDecayRateModifierEvent args)
    {
        if (!component.HadMangleness)
            return;

        switch (component.CurrentThirstThreshold)
        {
            case ThirstThreshold.OverHydrated:
                args.ActualDecayRate *= component.ManglenessDecayMultOverhydrated;
                break;
            case ThirstThreshold.Okay:
                args.ActualDecayRate *= component.ManglenessDecayMultOkay;
                break;
            case ThirstThreshold.Thirsty:
                args.ActualDecayRate *= component.ManglenessDecayMultThirsty;
                break;
        }
    }
}
