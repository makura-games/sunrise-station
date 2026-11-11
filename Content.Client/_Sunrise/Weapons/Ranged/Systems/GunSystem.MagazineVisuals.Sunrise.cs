using Content.Client.Weapons.Ranged.Components;
using Robust.Client.GameObjects;

// ReSharper disable once CheckNamespace
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
