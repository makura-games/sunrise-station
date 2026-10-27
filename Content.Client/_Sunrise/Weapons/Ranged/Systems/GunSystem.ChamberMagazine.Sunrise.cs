using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    partial void InitializeSunriseChamberMagazine() =>
        SubscribeLocalEvent<ChamberMagazineAmmoProviderComponent, AfterAutoHandleStateEvent>(OnSunriseChamberMagazineState);

    private void OnSunriseChamberMagazineState(Entity<ChamberMagazineAmmoProviderComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        if (!_sprite.LayerMapTryGet((ent.Owner, sprite), GunVisualLayers.Base, out var boltLayer, false))
            return;

        if (!Appearance.TryGetData(ent.Owner, AmmoVisuals.BoltClosed, out bool boltClosed))
            return;

        var prefix = string.IsNullOrEmpty(ent.Comp.SelectedPrefix) ? "" : $"_{ent.Comp.SelectedPrefix}";
        _sprite.LayerSetRsiState((ent.Owner, sprite), boltLayer, boltClosed ? $"base{prefix}" : $"bolt-open{prefix}");
    }
}
