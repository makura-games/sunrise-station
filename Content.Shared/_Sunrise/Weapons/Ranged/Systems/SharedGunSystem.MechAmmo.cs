using Content.Shared._Sunrise.Weapons.Ranged.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map;

// Файл намеренно расположен в _Sunrise, но расширяет ванильный partial-класс.
#pragma warning disable IDE0130
namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private SharedMechSystem _mech = default!;

    private void InitializeSunriseMechAmmo()
    {
        SubscribeLocalEvent<MechAmmoProviderComponent, EntGotInsertedIntoContainerMessage>(OnSunriseMechAmmoInserted);
        SubscribeLocalEvent<MechAmmoProviderComponent, EntGotRemovedFromContainerMessage>(OnSunriseMechAmmoRemoved);
        SubscribeLocalEvent<MechAmmoProviderComponent, TakeAmmoEvent>(OnSunriseMechTakeAmmo);
        SubscribeLocalEvent<MechAmmoProviderComponent, GetAmmoCountEvent>(OnSunriseMechAmmoCount);
    }

    private void OnSunriseMechAmmoInserted(Entity<MechAmmoProviderComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (!TryComp<MechComponent>(args.Container.Owner, out var mech)
            || args.Container != mech.EquipmentContainer
            || ent.Comp.Mech == args.Container.Owner)
            return;

        ent.Comp.Mech = args.Container.Owner;
        Dirty(ent);
    }

    private void OnSunriseMechAmmoRemoved(Entity<MechAmmoProviderComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (ent.Comp.Mech != args.Container.Owner)
            return;

        ent.Comp.Mech = null;
        Dirty(ent);
    }

    private void OnSunriseMechTakeAmmo(Entity<MechAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        if (!TryGetSunriseMechAmmoSource(ent.Comp, out var mech))
            return;

        var available = (int) (mech.Comp.Energy.Float() / ent.Comp.FireCost);
        var shots = Math.Min(available, args.Shots);
        for (var i = 0; i < shots; i++)
        {
            if (!_mech.TryChangeEnergy(mech, -ent.Comp.FireCost, mech.Comp))
                break;

            args.Ammo.Add(GetSunriseMechShootable(ent.Comp, args.Coordinates));
        }
    }

    private void OnSunriseMechAmmoCount(Entity<MechAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        if (!TryGetSunriseMechAmmoSource(ent.Comp, out var mech))
            return;

        args.Count = (int) (mech.Comp.Energy.Float() / ent.Comp.FireCost);
        args.Capacity = (int) (mech.Comp.MaxEnergy.Float() / ent.Comp.FireCost);
    }

    private bool TryGetSunriseMechAmmoSource(MechAmmoProviderComponent component, out Entity<MechComponent> mech)
    {
        mech = default;
        if (component.FireCost <= 0f || component.Mech is not { } mechUid)
            return false;

        if (!TryComp<MechComponent>(mechUid, out var mechComponent))
            return false;

        mech = (mechUid, mechComponent);
        return true;
    }

    private (EntityUid? Entity, IShootable Shootable) GetSunriseMechShootable(
        MechAmmoProviderComponent component,
        EntityCoordinates coordinates)
    {
        var projectile = Spawn(component.Proto, coordinates);
        return (projectile, EnsureShootable(projectile));
    }
}
