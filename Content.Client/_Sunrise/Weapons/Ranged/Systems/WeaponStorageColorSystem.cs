using Content.Client.Weapons.Ranged.Components;
using Content.Shared._Sunrise.Weapons.Ranged.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Weapons.Ranged.Systems;

public sealed partial class WeaponStorageColorSystem : VisualizerSystem<WeaponStorageColorComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, WeaponStorageColorComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData<string>(uid, WeaponStorageColorVisuals.ItemPrototype, out var id, args.Component) ||
            !AppearanceSystem.TryGetData<int>(uid, WeaponStorageColorVisuals.Fill, out var fill, args.Component))
            return;

        var color = Color.White;
        if (!component.UseStorage)
        {
            var weaponSprite = (uid, args.Sprite);
            if (!component.ColorMagazineStripe ||
                !SpriteSystem.LayerMapTryGet(weaponSprite, "strip", out var stripLayer, false))
                return;

            if (ProtoMan.TryIndex<EntityPrototype>(id, out var magazineProto) &&
                magazineProto.TryComp(out SpriteComponent? magazineSprite, Factory))
            {
                foreach (var layer in magazineSprite.AllLayers)
                {
                    if (layer is SpriteComponent.Layer spriteLayer && spriteLayer.State.ToString() == "stripe")
                    {
                        color = spriteLayer.Color;
                        break;
                    }
                }
            }

            SpriteSystem.LayerSetVisible(weaponSprite, stripLayer, fill != 0);
            if (fill != 0)
                SpriteSystem.LayerSetColor(weaponSprite, stripLayer, color);
            return;
        }
        if (ProtoMan.TryIndex<EntityPrototype>(id, out var proto) &&
            proto.TryComp(out SpriteComponent? source, Factory) &&
            (SpriteSystem.LayerMapTryGet((EntityUid.Invalid, source), AmmoVisualLayers.Tip, out var index, false) ||
             SpriteSystem.LayerMapTryGet((EntityUid.Invalid, source), GunVisualLayers.Tip, out index, false)))
        {
            color = source[index].Color;
        }

        var sprite = (uid, args.Sprite);
        if (component.SeparateBase)
            SpriteSystem.LayerSetVisible(sprite, "storage-fill", fill != 0);
        SpriteSystem.LayerSetVisible(sprite, "storage-tip", fill != 0);
        if (fill == 0)
            return;

        var state = fill < 0
            ? component.FullItemState ?? $"fill-{component.MaxFill}"
            : $"fill-{Math.Min(fill, component.MaxFill)}";
        if (component.SeparateBase)
            SpriteSystem.LayerSetRsiState(sprite, "storage-fill", state);
        SpriteSystem.LayerSetRsiState(sprite, "storage-tip", component.SeparateBase ? $"{state}-tip" : state);
        SpriteSystem.LayerSetColor(sprite, "storage-tip", color);
    }
}
