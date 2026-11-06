using System.Numerics;
using Content.Client._Sunrise.Sandbox.Access.Systems;
using Content.Client.Graphics;
using Content.Shared.Access.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Sandbox.Access.Overlays;

/// <summary>
/// Draws sprite-shaped outlines for mapping access readers.
/// </summary>
public sealed class MappingAccessOutlineOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> OutlineShaderPrototype = "SunriseMappingAccessOutline";
    private static readonly ProtoId<ShaderPrototype> UnshadedShaderPrototype = "unshaded";

    private const float OutlineWidth = 3.5f;

    private static readonly Color OutlineColor = Color.Aquamarine.WithAlpha(0.85f);

    private readonly IClyde _clyde;
    private readonly IEntityManager _ent;
    private readonly EntityQuery<PhysicsComponent> _physicsQuery;
    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private readonly MappingAccessReaderResolver _readerResolver;
    private readonly SpriteSystem _spriteSystem;
    private readonly MappingAccessTightBounds _tightBounds;
    private readonly SharedTransformSystem _transform;
    private readonly ShaderInstance _outlineShader;
    private readonly ShaderInstance _unshadedShader;

    public MappingAccessBodyFilter BodyFilter { get; set; } = MappingAccessBodyFilter.Both;
    public bool ElectronicsOnly { get; set; }

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    internal MappingAccessOutlineOverlay(
        IEntityManager entityManager,
        SpriteSystem spriteSystem,
        IPrototypeManager prototypeManager,
        IClyde clyde,
        MappingAccessReaderResolver readerResolver,
        MappingAccessTightBounds tightBounds,
        EntityQuery<PhysicsComponent> physicsQuery)
    {
        _clyde = clyde;
        _ent = entityManager;
        _physicsQuery = physicsQuery;
        _readerResolver = readerResolver;
        _spriteSystem = spriteSystem;
        _tightBounds = tightBounds;
        _transform = _ent.System<SharedTransformSystem>();
        _outlineShader = prototypeManager.Index(OutlineShaderPrototype).InstanceUnique();
        _unshadedShader = prototypeManager.Index(UnshadedShaderPrototype).InstanceUnique();
        ZIndex = 0;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
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
                name: "mapping-access-outline-mask");
        }

        var targetScale = res.MaskTarget.Size / (Vector2) args.Viewport.Size;
        var scale = args.Viewport.RenderScale / (Vector2.One / targetScale);
        var worldHandle = args.WorldHandle;
        var mapId = args.MapId;
        var worldAabb = args.WorldAABB;
        var eyeRotation = eye.Rotation;

        worldHandle.RenderInRenderTarget(res.MaskTarget,
            () =>
        {
            worldHandle.UseShader(_unshadedShader);

            var worldToTexture = res.MaskTarget.GetWorldToLocalMatrix(eye, scale);
            var query = _ent.AllEntityQueryEnumerator<AccessReaderComponent, SpriteComponent, TransformComponent>();

            while (query.MoveNext(out var uid, out var accessReader, out var sprite, out var transform))
            {
                if (!TryGetOutlineData(uid, accessReader, sprite, transform, mapId, worldAabb, eyeRotation, out var worldPos, out var worldRot))
                    continue;

                DrawSpriteMask((uid, sprite), worldPos, worldRot, worldToTexture, worldHandle, eyeRotation);
            }

            worldHandle.SetTransform(Matrix3x2.Identity);
            worldHandle.UseShader(null);
        },
            Color.Transparent);

        _outlineShader.SetParameter("outline_width", OutlineWidth);
        _outlineShader.SetParameter("outline_color", OutlineColor);

        args.WorldHandle.SetTransform(Matrix3x2.Identity);
        args.WorldHandle.UseShader(_outlineShader);
        args.WorldHandle.DrawTextureRect(res.MaskTarget.Texture, args.WorldBounds);
        args.WorldHandle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _outlineShader.Dispose();
        _unshadedShader.Dispose();
        _resources.Dispose();

        base.DisposeBehavior();
    }

    private bool TryGetOutlineData(
        EntityUid uid,
        AccessReaderComponent accessReader,
        SpriteComponent sprite,
        TransformComponent transform,
        MapId mapId,
        Box2 worldAabb,
        Angle eyeRotation,
        out Vector2 worldPos,
        out Angle worldRot)
    {
        worldPos = default;
        worldRot = default;

        if (transform.MapID != mapId ||
            !accessReader.Enabled ||
            !sprite.Visible)
        {
            return false;
        }

        if (!_readerResolver.TryGetDisplayedAccessReader(uid, accessReader, ElectronicsOnly, out var displayedReader) ||
            !displayedReader.Enabled ||
            displayedReader.AccessLists.Count == 0)
        {
            return false;
        }

        if (!_physicsQuery.TryComp(uid, out var physics) ||
            !MatchesBodyFilter(physics.BodyType, BodyFilter))
        {
            return false;
        }

        (worldPos, worldRot) = _transform.GetWorldPositionRotation(transform);
        var worldBounds = _tightBounds.TryGetSpriteWorldAabb((uid, sprite), worldPos, worldRot, eyeRotation, out var tightBounds)
            ? tightBounds
            : _spriteSystem.CalculateBounds((uid, sprite), worldPos, worldRot, eyeRotation).CalcBoundingBox();

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
        var overrideDirection = sprite.Comp.EnableDirectionOverride ? sprite.Comp.DirectionOverride : (Direction?) null;

        var angle = (worldRot + eyeRotation).Reduced().FlipPositive();
        var cardinal = Angle.Zero;

        if (!sprite.Comp.NoRotation && sprite.Comp.SnapCardinals)
            cardinal = angle.RoundToCardinalAngle();

        var entityMatrix = Matrix3Helpers.CreateTransform(
            worldPos,
            sprite.Comp.NoRotation ? -eyeRotation : worldRot - cardinal);
        var spriteMatrix = Matrix3x2.Multiply(sprite.Comp.LocalMatrix, entityMatrix);
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

    private static bool MatchesBodyFilter(BodyType bodyType, MappingAccessBodyFilter filter)
    {
        return filter switch
        {
            MappingAccessBodyFilter.Static => (bodyType & BodyType.Static) != 0,
            MappingAccessBodyFilter.Dynamic => (bodyType & BodyType.Dynamic) != 0,
            _ => (bodyType & (BodyType.Static | BodyType.Dynamic)) != 0,
        };
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
