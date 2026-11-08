using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Random;

// ReSharper disable once CheckNamespace
namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private void InitializeSunrise()
    {
        SubscribeLocalEvent<ChamberMagazineAmmoProviderComponent, MapInitEvent>(OnChamberMagazineMapInit);
    }

    private void OnChamberMagazineMapInit(Entity<ChamberMagazineAmmoProviderComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.SelectedPrefix is not null || ent.Comp.AvailablePrefixes.Count == 0)
            return;

        ent.Comp.SelectedPrefix = Random.Pick(ent.Comp.AvailablePrefixes);
        Dirty(ent);
    }
}
