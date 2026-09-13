using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.ComponentTrees;
using Robust.Shared.Enums;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Shaders.Bloom;

/// <summary>
/// Draws a bloom mask for compatible point lights.
/// </summary>
public sealed class PointLightingOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> BloomShader = "SunriseLightingOverlay";

    private readonly BloomOverlayTreeSystem _bloomTree;
    private readonly Dictionary<BloomMaskKey, BloomMaskData> _maskCache = [];
    private readonly EntityQuery<PointLightComponent> _pointLightQuery;
    private readonly ShaderInstance _shader;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly float _baseHaze;
    private readonly float _hazeDivisor;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    public override bool RequestScreenTexture => true;

    private readonly List<BloomLightEntry> _visibleLights = [];
    public float BloomStrength;

    public PointLightingOverlay(
        BloomOverlayTreeSystem bloomTree,
        IPrototypeManager prototypeManager,
        SpriteSystem spriteSystem,
        TransformSystem transform,
        EntityQuery<PointLightComponent> pointLightQuery,
        int zIndex,
        float baseHaze,
        float hazeDivisor,
        float strength)
    {
        _bloomTree = bloomTree;
        _shader = prototypeManager.Index(BloomShader).InstanceUnique();
        _sprite = spriteSystem;
        _transform = transform;
        _pointLightQuery = pointLightQuery;
        _baseHaze = baseHaze;
        _hazeDivisor = hazeDivisor;
        BloomStrength = strength;
        ZIndex = zIndex;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (BloomStrength <= 0f)
            return false;

        _visibleLights.Clear();
        var visibleArea = args.WorldAABB.Enlarged(1f);
        var queryState = new BloomLightQueryState(
            _visibleLights,
            _maskCache,
            _pointLightQuery,
            _sprite,
            _transform);
        _bloomTree.QueryAabb(ref queryState, CollectBloomLight, args.MapId, visibleArea);
        return _visibleLights.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("base_haze", _baseHaze);
        _shader.SetParameter("haze_divisor", _hazeDivisor / BloomStrength);
        handle.UseShader(_shader);

        foreach (var light in _visibleLights)
        {
            handle.SetTransform(light.WorldMatrix);
            handle.DrawTexture(light.MaskTexture, light.MaskOffset, light.Color);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
        _shader.Dispose();
        base.DisposeBehavior();
    }

    private static bool CollectBloomLight(
        ref BloomLightQueryState queryState,
        in ComponentTreeEntry<BloomOverlayVisualsComponent> bloomEntry)
    {
        var bloomVisuals = bloomEntry.Component;
        if (!queryState.PointLightQuery.TryComp(bloomEntry.Uid, out var pointLight) ||
            !pointLight.Enabled)
            return true;

        var transform = bloomEntry.Transform;
        var (_, _, worldMatrix) = queryState.Transform.GetWorldPositionRotationMatrix(transform);

        var maskKey = new BloomMaskKey(bloomVisuals.MaskSprite, bloomVisuals.MaskOffset);
        if (!queryState.MaskCache.TryGetValue(maskKey, out var mask))
        {
            var texture = queryState.Sprite.Frame0(bloomVisuals.MaskSprite);
            mask = new BloomMaskData(
                texture,
                bloomVisuals.MaskOffset - new Vector2(texture.Width, texture.Height) / (2f * EyeManager.PixelsPerMeter));
            queryState.MaskCache.Add(maskKey, mask);
        }

        queryState.VisibleLights.Add(new BloomLightEntry(
            worldMatrix,
            mask.Texture,
            mask.Offset,
            pointLight.Color * bloomVisuals.BloomColor));

        return true;
    }

    private readonly record struct BloomLightQueryState(
        List<BloomLightEntry> VisibleLights,
        Dictionary<BloomMaskKey, BloomMaskData> MaskCache,
        EntityQuery<PointLightComponent> PointLightQuery,
        SpriteSystem Sprite,
        TransformSystem Transform);

    private readonly record struct BloomMaskKey(SpriteSpecifier Sprite, Vector2 Offset);

    private readonly record struct BloomMaskData(Texture Texture, Vector2 Offset);

    private readonly record struct BloomLightEntry(
        Matrix3x2 WorldMatrix,
        Texture MaskTexture,
        Vector2 MaskOffset,
        Color Color);
}
