using Content.Client.Weapons.Ranged.Components;
using Robust.Client.GameObjects;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private void SetMagazineTipVisuals(
        Entity<MagazineVisualsComponent> ent,
        SpriteComponent sprite,
        int step,
        bool visible)
    {
        if (!_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.Tip, out _, false))
            return;

        if (visible)
            _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.Tip, $"{ent.Comp.MagState}-tip-{step}");

        _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Tip, visible);
    }
}
