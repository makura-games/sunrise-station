using Content.Shared._Sunrise.Weapons.Ranged;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Weapons.Ranged;

public sealed class BulletHoleVisualizerSystem : VisualizerSystem<BulletHoleComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly ResPath RsiPath = new("/Textures/_Sunrise/Effects/bulletholes.rsi");
    private const string BulletHoleLayerPrefix = "bullet-hole-";
    private const int MaxBulletHoles = 24;

    protected override void OnAppearanceChange(EntityUid uid, BulletHoleComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        if (!AppearanceSystem.TryGetData<BulletHoleVisualsData>(uid, BulletHoleVisuals.Holes, out var data, args.Component))
            return;

        var holes = data.Holes;
        for (var i = 0; i < holes.Count; i++)
        {
            var layer = _sprite.LayerMapReserve((uid, sprite), BulletHoleLayerPrefix + i);
            var hole = holes[i];
            _sprite.LayerSetVisible((uid, sprite), layer, true);
            _sprite.LayerSetRsi((uid, sprite), layer, RsiPath);
            _sprite.LayerSetRsiState((uid, sprite), layer, hole.State);
            _sprite.LayerSetOffset((uid, sprite), layer, hole.Offset);
            _sprite.LayerSetRotation((uid, sprite), layer, hole.Rotation);
        }

        for (var i = holes.Count; i < MaxBulletHoles; i++)
        {
            if (_sprite.LayerMapTryGet((uid, sprite), BulletHoleLayerPrefix + i, out var layer, false))
                _sprite.LayerSetVisible((uid, sprite), layer, false);
        }
    }
}
