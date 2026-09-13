using System.Numerics;
using Content.Shared._Sunrise.ScaleSprite;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.ScaleSprite;

public sealed partial class ScaleSpriteSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScaleSpriteComponent, AppearanceChangeEvent>(OnChangeData);
    }

    private void OnChangeData(EntityUid uid, ScaleSpriteComponent component, ref AppearanceChangeEvent ev)
    {
        if (!ev.AppearanceData.TryGetValue(ScaleSpriteVisuals.Scale, out var scale) || ev.Sprite == null)
            return;

        _sprite.SetScale((uid, ev.Sprite), (Vector2)scale);
    }
}
