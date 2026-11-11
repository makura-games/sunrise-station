using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Physics;

namespace Content.Client._Sunrise.Shaders.Bloom;

/// <summary>
/// Maintains a spatial tree containing only lights compatible with the bloom overlay.
/// </summary>
public sealed partial class BloomOverlayTreeSystem : ComponentTreeSystem<BloomOverlayTreeComponent, BloomOverlayVisualsComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;

    protected override bool DoFrameUpdate => true;
    protected override bool DoTickUpdate => false;
    protected override bool Recursive => true;

    protected override Box2 ExtractAabb(
        in ComponentTreeEntry<BloomOverlayVisualsComponent> entry,
        Vector2 pos,
        Angle rot)
    {
        var texture = _sprite.Frame0(entry.Component.MaskSprite);
        var size = new Vector2(texture.Width, texture.Height) / EyeManager.PixelsPerMeter;
        var radius = size.Length() / 2f + entry.Component.MaskOffset.Length();
        var extents = new Vector2(radius);
        return new Box2(pos - extents, pos + extents);
    }
}
