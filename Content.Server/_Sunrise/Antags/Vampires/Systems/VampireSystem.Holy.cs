using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Prayer;
using Content.Shared.Popups;
using Robust.Server.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Урон от святынь.

    [Dependency] private readonly ContainerSystem _container = default!;

    private void HandleHolyWater(Entity<VampireComponent> ent)
    {
        if (!TryComp<VampireFeedingComponent>(ent, out var feeding) ||
            !TryComp<VampireHolyComponent>(ent, out var holy) ||
            feeding.UniqueVictims < holy.RequiredVictims)
        {
            return;
        }

        if (_timing.CurTime < holy.NextHolyWaterEffect)
            return;

        var holyWater = _solution.GetTotalPrototypeQuantity(ent.Owner, holy.HolyWaterReagent);
        if (holyWater <= FixedPoint2.Zero ||
            TryComp<MobStateComponent>(ent, out var mobState) && mobState.CurrentState == Shared.Mobs.MobState.Dead)
        {
            return;
        }

        holy.NextHolyWaterEffect = _timing.CurTime + holy.EffectInterval;

        if (ent.Comp.DrunkBlood > 0)
        {
            TrySpendBlood(
                ent,
                Math.Min(holy.HolyWaterBloodDrain, ent.Comp.DrunkBlood),
                showPopup: false);
            ApplyDistributedDamage(
                ent.Owner,
                holy.BruteDamageTypes,
                FixedPoint2.New(holy.HolyWaterBruteDamage));

            if (TryComp<StaminaComponent>(ent, out var stamina))
                _stamina.TakeStaminaDamage(ent.Owner, holy.HolyWaterStaminaDamage, stamina);

            return;
        }

        ApplyDistributedDamage(
            ent.Owner,
            holy.BurnDamageTypes,
            FixedPoint2.New(holy.HolyWaterBurnDamage));
    }

    private void HandleHolyPlace(Entity<VampireComponent> ent)
    {
        if (!TryComp<VampireFeedingComponent>(ent, out var feeding) ||
            !TryComp<VampireHolyComponent>(ent, out var holy) ||
            feeding.UniqueVictims < holy.RequiredVictims)
        {
            return;
        }

        if (_timing.CurTime < holy.NextHolyPlaceEffect ||
            !IsInHolyPlace(ent, holy.HolyPlaceRange) ||
            TryComp<MobStateComponent>(ent, out var mobState) && mobState.CurrentState == Shared.Mobs.MobState.Dead)
        {
            return;
        }

        holy.NextHolyPlaceEffect = _timing.CurTime + holy.EffectInterval;

        if (_timing.CurTime >= holy.NextHolyPlacePopup)
        {
            _popup.PopupEntity(
                Loc.GetString("vampire-holy-place-burn"),
                ent.Owner,
                ent.Owner,
                PopupType.MediumCaution);
            holy.NextHolyPlacePopup = _timing.CurTime + holy.PopupInterval;
        }

        if (!_prototype.TryIndex(holy.HolyPlaceDamageType, out var damageType))
            return;

        var damage = new DamageSpecifier(damageType, FixedPoint2.New(holy.HolyPlaceDamage));
        _damageable.TryChangeDamage(ent.Owner, damage, true);
    }

    private void ApplyDistributedDamage(
        EntityUid uid,
        IReadOnlyList<ProtoId<DamageTypePrototype>> damageTypes,
        FixedPoint2 amount)
    {
        _damageable.TryChangeDamage(uid, CreateDistributedDamage(damageTypes, amount), true);
    }

    private bool IsInHolyPlace(EntityUid uid, float range)
    {
        if (_container.IsEntityInContainer(uid))
            return false;

        var coordinates = Transform(uid).Coordinates;
        foreach (var target in _lookup.GetEntitiesInRange(coordinates, range, LookupFlags.Static))
        {
            if (target != uid &&
                HasComp<PrayableComponent>(target) &&
                Transform(target).Anchored &&
                _interaction.InRangeUnobstructed(uid, target, range))
            {
                return true;
            }
        }

        return false;
    }

    private static DamageSpecifier CreateDistributedDamage(
        IReadOnlyList<ProtoId<DamageTypePrototype>> damageTypes,
        FixedPoint2 amount)
    {
        var damage = new DamageSpecifier();
        var remaining = amount;

        for (var i = 0; i < damageTypes.Count; i++)
        {
            var value = remaining / FixedPoint2.New(damageTypes.Count - i);
            damage.DamageDict.Add(damageTypes[i], value);
            remaining -= value;
        }

        return damage;
    }
}
