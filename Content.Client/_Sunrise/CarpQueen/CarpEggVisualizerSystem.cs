using Content.Shared._Sunrise.CarpQueen;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._Sunrise.CarpQueen;

public sealed partial class CarpEggVisualizerSystem : VisualizerSystem<CarpEggComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, CarpEggComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.AppearanceData.TryGetValue(CarpEggVisuals.OverlayColor, out var obj))
            return;

        var color = (Color) obj;
        var sprite = args.Sprite;
        if (_sprite.LayerMapTryGet((uid, sprite), "overlay", out var layer, false))
        {
            _sprite.LayerSetColor((uid, sprite), layer, color);
        }
    }
}


