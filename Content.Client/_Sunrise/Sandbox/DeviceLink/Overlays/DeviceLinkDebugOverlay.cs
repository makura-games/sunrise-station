using System.Numerics;
using Content.Client._Sunrise.Sandbox.DeviceLink.Systems;
using Content.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Sandbox.DeviceLink.Overlays;

public sealed partial class DeviceLinkDebugOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> OutlineShaderPrototype = "SunriseMappingAccessOutline";
    private static readonly ProtoId<ShaderPrototype> UnshadedShaderPrototype = "unshaded";

    private const float OutlineWidth = 3.5f;
    private const float RayHalfWidth = 0.02f;
    private const float SourceMarkerRadius = 0.1f;

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _ent = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly DeviceLinkOverlaySystem _deviceLinking;
    private readonly EntityQuery<SpriteComponent> _spriteQuery;
    private readonly EntityQuery<TransformComponent> _transformQuery;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly Dictionary<Color, HashSet<EntityUid>> _outlineGroups = new();
    private readonly Vector2[] _rayVertices = new Vector2[4];
    private readonly ShaderInstance _outlineShader;
    private readonly ShaderInstance _unshadedShader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public DeviceLinkDebugOverlay()
    {
        IoCManager.InjectDependencies(this);

        _deviceLinking = _ent.System<DeviceLinkOverlaySystem>();
        _spriteQuery = _ent.GetEntityQuery<SpriteComponent>();
        _transformQuery = _ent.GetEntityQuery<TransformComponent>();
        _sprite = _ent.System<SpriteSystem>();
        _transform = _ent.System<TransformSystem>();
        _outlineShader = _prototype.Index(OutlineShaderPrototype).InstanceUnique();
        _unshadedShader = _prototype.Index(UnshadedShaderPrototype).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye != null && _deviceLinking.Rays.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var rays = _deviceLinking.Rays;
        if (rays.Count == 0 || args.Viewport.Eye == null)
            return;

        if (args.Space != OverlaySpace.WorldSpace)
            return;

        var colors = _deviceLinking.SourceColors;
        BuildOutlineGroups(rays, colors);
        DrawRays(args, rays, colors);
        DrawOutlines(args);
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();

        _outlineShader.Dispose();
        _unshadedShader.Dispose();
        _resources.Dispose();
    }

    private void BuildOutlineGroups(
        Dictionary<EntityUid, List<EntityUid>> rays,
        Dictionary<EntityUid, Color> colors)
    {
        foreach (var entities in _outlineGroups.Values)
        {
            entities.Clear();
        }

        foreach (var (source, connections) in rays)
        {
            var rayColor = colors.TryGetValue(source, out var color)
                ? color
                : Color.White;

            if (!_outlineGroups.TryGetValue(rayColor, out var entities))
            {
                entities = [];
                _outlineGroups.Add(rayColor, entities);
            }

            entities.Add(source);

            foreach (var connection in connections)
            {
                entities.Add(connection);
            }
        }
    }

    private void DrawRays(
        in OverlayDrawArgs args,
        Dictionary<EntityUid, List<EntityUid>> rays,
        Dictionary<EntityUid, Color> colors)
    {
        foreach (var (source, connections) in rays)
        {
            if (!_transformQuery.TryComp(source, out var sourceTransform) ||
                !sourceTransform.MapUid.HasValue)
            {
                continue;
            }

            var rayColor = colors.TryGetValue(source, out var color)
                ? color
                : Color.White;
            var sourcePos = _transform.GetWorldPosition(sourceTransform);
            args.WorldHandle.DrawCircle(sourcePos, SourceMarkerRadius, rayColor);

            foreach (var connection in connections)
            {
                if (!_transformQuery.TryComp(connection, out var destinationTransform) ||
                    !destinationTransform.MapUid.HasValue)
                {
                    continue;
                }

                var destinationPos = _transform.GetWorldPosition(destinationTransform);
                DrawThickLine(args.WorldHandle, sourcePos, destinationPos, rayColor);
            }
        }
    }

    private void DrawOutlines(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        if (eye == null)
            return;

        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());
        var targetSize = args.Viewport.RenderTarget.Size;

        if (res.MaskTarget?.Texture.Size != targetSize)
        {
            res.MaskTarget?.Dispose();
            res.MaskTarget = _clyde.CreateRenderTarget(
                targetSize,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                name: "device-link-outline-mask");
        }

        var targetScale = res.MaskTarget.Size / (Vector2) args.Viewport.Size;
        var scale = args.Viewport.RenderScale / (Vector2.One / targetScale);
        var worldHandle = args.WorldHandle;
        var worldToTexture = res.MaskTarget.GetWorldToLocalMatrix(eye, scale);
        var eyeRotation = eye.Rotation;
        var mapId = args.MapId;
        var worldAabb = args.WorldAABB;

        foreach (var (color, entities) in _outlineGroups)
        {
            if (entities.Count == 0)
                continue;

            worldHandle.RenderInRenderTarget(res.MaskTarget,
                () =>
                {
                    worldHandle.UseShader(_unshadedShader);

                    foreach (var uid in entities)
                    {
                        if (!TryGetOutlineData(uid, mapId, worldAabb, eyeRotation, out var sprite, out var worldPos, out var worldRot))
                            continue;

                        DrawSpriteMask((uid, sprite), worldPos, worldRot, worldToTexture, worldHandle, eyeRotation);
                    }

                    worldHandle.SetTransform(Matrix3x2.Identity);
                    worldHandle.UseShader(null);
                },
                Color.Transparent);

            _outlineShader.SetParameter("outline_width", OutlineWidth);
            _outlineShader.SetParameter("outline_color", color);

            worldHandle.SetTransform(Matrix3x2.Identity);
            worldHandle.UseShader(_outlineShader);
            worldHandle.DrawTextureRect(res.MaskTarget.Texture, args.WorldBounds);
            worldHandle.UseShader(null);
        }
    }

    private bool TryGetOutlineData(
        EntityUid uid,
        MapId mapId,
        Box2 worldAabb,
        Angle eyeRotation,
        out SpriteComponent sprite,
        out Vector2 worldPos,
        out Angle worldRot)
    {
        sprite = default!;
        worldPos = default;
        worldRot = default;

        if (!_spriteQuery.TryComp(uid, out var spriteComponent) ||
            !_transformQuery.TryComp(uid, out var transform) ||
            transform.MapID != mapId ||
            !spriteComponent.Visible)
        {
            return false;
        }

        sprite = spriteComponent;
        (worldPos, worldRot) = _transform.GetWorldPositionRotation(transform);
        var worldBounds = _sprite.CalculateBounds((uid, sprite), worldPos, worldRot, eyeRotation).CalcBoundingBox();
        return worldBounds.Intersects(in worldAabb);
    }

    private void DrawSpriteMask(
        Entity<SpriteComponent> sprite,
        Vector2 worldPos,
        Angle worldRot,
        Matrix3x2 worldToTexture,
        DrawingHandleWorld worldHandle,
        Angle eyeRotation)
    {
        var overrideDirection = sprite.Comp.EnableDirectionOverride
            ? sprite.Comp.DirectionOverride
            : (Direction?) null;

        var angle = (worldRot + eyeRotation).Reduced().FlipPositive();
        var cardinal = Angle.Zero;

        if (!sprite.Comp.NoRotation && sprite.Comp.SnapCardinals)
            cardinal = angle.RoundToCardinalAngle();

        var spriteMatrix = Matrix3Helpers.CreateTransform(
            worldPos,
            sprite.Comp.NoRotation ? -eyeRotation : worldRot - cardinal);
        spriteMatrix = Matrix3x2.Multiply(sprite.Comp.LocalMatrix, spriteMatrix);
        spriteMatrix = Matrix3x2.Multiply(spriteMatrix, worldToTexture);

        if (!sprite.Comp.GranularLayersRendering)
        {
            foreach (var layerObject in sprite.Comp.AllLayers)
            {
                if (layerObject is not SpriteComponent.Layer layer)
                    continue;

                DrawLayerMask(layer, worldHandle, ref spriteMatrix, angle, overrideDirection, sprite.Comp.Color);
            }

            return;
        }

        var defaultMatrix = Matrix3x2.Multiply(
            Matrix3x2.Multiply(sprite.Comp.LocalMatrix, Matrix3Helpers.CreateTransform(worldPos, worldRot)),
            worldToTexture);
        var snapMatrix = Matrix3x2.Multiply(
            Matrix3x2.Multiply(sprite.Comp.LocalMatrix, Matrix3Helpers.CreateTransform(worldPos, worldRot - angle.RoundToCardinalAngle())),
            worldToTexture);
        var noRotationMatrix = Matrix3x2.Multiply(
            Matrix3x2.Multiply(sprite.Comp.LocalMatrix, Matrix3Helpers.CreateTransform(worldPos, -eyeRotation)),
            worldToTexture);

        foreach (var layerObject in sprite.Comp.AllLayers)
        {
            if (layerObject is not SpriteComponent.Layer layer)
                continue;

            switch (layer.RenderingStrategy)
            {
                case LayerRenderingStrategy.Default:
                    DrawLayerMask(layer, worldHandle, ref defaultMatrix, angle, overrideDirection, sprite.Comp.Color);
                    break;
                case LayerRenderingStrategy.NoRotation:
                    DrawLayerMask(layer, worldHandle, ref noRotationMatrix, angle, overrideDirection, sprite.Comp.Color);
                    break;
                case LayerRenderingStrategy.SnapToCardinals:
                    DrawLayerMask(layer, worldHandle, ref snapMatrix, angle, overrideDirection, sprite.Comp.Color);
                    break;
                default:
                    DrawLayerMask(layer, worldHandle, ref spriteMatrix, angle, overrideDirection, sprite.Comp.Color);
                    break;
            }
        }
    }

    private static void DrawLayerMask(
        SpriteComponent.Layer layer,
        DrawingHandleWorld worldHandle,
        ref Matrix3x2 spriteMatrix,
        Angle angle,
        Direction? overrideDirection,
        Color spriteColor)
    {
        if (!layer.Visible || layer.Blank || layer.CopyToShaderParameters != null)
            return;

        var state = layer.ActualState;
        var dir = state == null
            ? RsiDirection.South
            : SpriteComponent.Layer.GetDirection(state.RsiDirections, angle);

        if (overrideDirection != null && state != null)
            dir = overrideDirection.Value.Convert(state.RsiDirections);

        dir = dir.OffsetRsiDir(layer.DirOffset);

        var texture = state?.GetFrame(dir, layer.AnimationFrame) ?? layer.Texture;
        if (texture == null)
            return;

        layer.GetLayerDrawMatrix(dir, out var layerMatrix);
        var transformMatrix = Matrix3x2.Multiply(layerMatrix, spriteMatrix);
        var modulate = spriteColor * layer.Color;

        if (modulate.A <= 0f)
            return;

        worldHandle.SetTransform(transformMatrix);

        var textureSize = texture.Size / (float) EyeManager.PixelsPerMeter;
        var quad = Box2.FromDimensions(textureSize / -2, textureSize);
        worldHandle.DrawTextureRectRegion(texture, quad, Color.White.WithAlpha(modulate.A));
    }

    private void DrawThickLine(
        DrawingHandleWorld worldHandle,
        Vector2 from,
        Vector2 to,
        Color color)
    {
        var direction = to - from;
        if (direction.LengthSquared() <= 0.0001f)
            return;

        var offset = Vector2.Normalize(new Vector2(-direction.Y, direction.X)) * RayHalfWidth;
        _rayVertices[0] = from - offset;
        _rayVertices[1] = from + offset;
        _rayVertices[2] = to - offset;
        _rayVertices[3] = to + offset;

        worldHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _rayVertices, color);
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? MaskTarget;

        public void Dispose()
        {
            MaskTarget?.Dispose();
        }
    }
}
