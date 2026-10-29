using System.Numerics;
using Content.Client._Sunrise.MarkingEffectsClient;
using Content.Shared._Sunrise.MarkingEffects;
using Content.Shared.Body;
using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Body;

public sealed partial class VisualBodySystem
{
    public void ApplySunriseMarkingEffects(Entity<VisualOrganMarkingsComponent> ent, EntityUid target)
    {
        if (!TryComp<SpriteComponent>(target, out var spriteComp))
            return;

        Entity<SpriteComponent> spriteEntity = (target, spriteComp);
        var bodyTypeVisualKey = GetBodyTypeVisualKey(target);
        var sex = GetSex(target);
        var pendingDisplacements = new List<(string LayerId, HumanoidVisualLayers Layer, ShaderInstance? Shader)>();

        foreach (var marking in ent.Comp.AppliedMarkings)
        {
            if (!_marking.TryGetMarking(marking, out var proto))
                continue;

            for (var i = 0; i < proto.Sprites.Count; i++)
            {
                var spriteSpec = proto.Sprites[i];

                DebugTools.Assert(spriteSpec is SpriteSpecifier.Rsi);
                if (spriteSpec is not SpriteSpecifier.Rsi rsi)
                    continue;

                var layerId = $"{proto.ID}-{rsi.RsiState}";

                if (!_sprite.LayerMapTryGet(target, layerId, out var layerIndex, false))
                    continue;

                var shader = GetMarkingShader(marking, i, spriteEntity, layerIndex, out var shaderPrototype);
                spriteComp.LayerSetShader(layerIndex, shader, shaderPrototype);

                if (shader is not null)
                    _sprite.LayerSetColor(target, layerId, Color.White);
                else if (marking.MarkingColors is not null && i < marking.MarkingColors.Count)
                    _sprite.LayerSetColor(target, layerId, marking.MarkingColors[i]);
                else
                    _sprite.LayerSetColor(target, layerId, Color.White);

                pendingDisplacements.Add((layerId, proto.BodyPart, shader));
            }
        }

        foreach (var (layerId, layer, shader) in pendingDisplacements)
        {
            ApplyMarkingDisplacement(spriteEntity,
                layerId,
                layer,
                ent.Comp,
                bodyTypeVisualKey,
                sex,
                shader);
        }
    }

    public void RemoveSunriseMarkingDisplacement(EntityUid target, string layerId)
    {
        if (!TryComp<SpriteComponent>(target, out var spriteComp))
            return;

        _displacement.EnsureDisplacementIsNotOnSprite((target, spriteComp), layerId);
    }

    public void SetSunriseMarkingDisplacementVisible(EntityUid target, string layerId, bool visible)
    {
        var displacementLayerId = $"{layerId}-displacement";
        if (_sprite.LayerMapTryGet(target, displacementLayerId, out var displacementIndex, false))
            _sprite.LayerSetVisible(target, displacementIndex, visible);
    }

    private void UpdateSunriseMarkingVisibility(
        Entity<VisualOrganMarkingsComponent> ent,
        EntityUid target,
        HumanoidVisualLayers layer,
        bool visible)
    {
        ent.Comp.DependentHidingLayers.TryGetValue(layer, out var dependentLayers);

        foreach (var marking in ent.Comp.AppliedMarkings)
        {
            if (!_marking.TryGetMarking(marking, out var proto))
                continue;

            var visualLayer = ResolveSunriseMarkingVisualLayer(proto.BodyPart);
            if (visualLayer != layer && dependentLayers?.Contains(proto.BodyPart) != true)
                continue;

            var layerVisible = IsSunriseLayerVisible(target, visualLayer, visible);
            foreach (var sprite in proto.Sprites)
            {
                DebugTools.Assert(sprite is SpriteSpecifier.Rsi);
                if (sprite is not SpriteSpecifier.Rsi rsi)
                    continue;

                var layerId = $"{proto.ID}-{rsi.RsiState}";
                if (!_sprite.LayerMapTryGet(target, layerId, out var index, true))
                    continue;

                _sprite.LayerSetVisible(target, index, layerVisible);
                SetSunriseMarkingDisplacementVisible(target, layerId, layerVisible);
            }
        }
    }

    /// <summary>
    /// Старый слой дополнительных кошачьих ушей не имеет отдельного anchor в NuBody.
    /// Визуально он совпадает со слоем верхних частей головы.
    /// </summary>
    private static HumanoidVisualLayers ResolveSunriseMarkingVisualLayer(HumanoidVisualLayers layer)
    {
        return layer == HumanoidVisualLayers.Special
            ? HumanoidVisualLayers.HeadTop
            : layer;
    }

    private bool HasSunriseAliasedMarkingLayer(
        Entity<VisualOrganMarkingsComponent> ent,
        HumanoidVisualLayers visualLayer)
    {
        foreach (var markings in ent.Comp.Markings.Values)
        {
            foreach (var marking in markings)
            {
                if (_marking.TryGetMarking(marking, out var proto) &&
                    proto.BodyPart != visualLayer &&
                    ResolveSunriseMarkingVisualLayer(proto.BodyPart) == visualLayer)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private ShaderInstance? GetMarkingShader(
        Marking marking,
        int index,
        Entity<SpriteComponent> sprite,
        int layerIndex,
        out string? shaderPrototype)
    {
        shaderPrototype = null;
        if (marking.MarkingEffects is null || marking.MarkingEffects.Count <= index)
            return null;

        var effect = marking.MarkingEffects[index];
        shaderPrototype = effect.Type switch
        {
            MarkingEffectType.Gradient => "Gradient",
            MarkingEffectType.RoughGradient => "RoughGradient",
            _ => null,
        };

        if (shaderPrototype is null ||
            !_prototype.TryIndex<ShaderPrototype>(shaderPrototype, out var shader))
        {
            return null;
        }

        var instance = shader.InstanceUnique();
        instance.ApplyShaderParams(effect, GetTextureScale(sprite, layerIndex));
        return instance;
    }

    private Vector2 GetTextureScale(Entity<SpriteComponent> sprite, int layerIndex)
    {
        var rsi = _sprite.LayerGetEffectiveRsi(sprite.AsNullable(), layerIndex);
        if (rsi is null)
            return new Vector2(EyeManager.PixelsPerMeter, EyeManager.PixelsPerMeter);

        return new Vector2(rsi.Size.X, rsi.Size.Y);
    }

    private void ApplyMarkingDisplacement(
        Entity<SpriteComponent> sprite,
        string layerId,
        HumanoidVisualLayers layer,
        VisualOrganMarkingsComponent markings,
        string? bodyTypeVisualKey,
        Sex sex,
        ShaderInstance? shader)
    {
        if (!_sprite.LayerMapTryGet(sprite.AsNullable(), layerId, out var layerIndex, false))
            return;

        var displacement = GetMarkingDisplacement(markings, layer, bodyTypeVisualKey, sex);

        if (displacement is null)
        {
            _displacement.EnsureDisplacementIsNotOnSprite(sprite, layerId);
            return;
        }

        if (!_displacement.TryAddDisplacement(displacement, sprite, layerIndex, layerId, out var displacementKey, shader))
            return;

        var visualLayer = ResolveSunriseMarkingVisualLayer(layer);
        _sprite.LayerSetVisible(sprite.Owner, displacementKey, IsSunriseLayerVisible(sprite.Owner, visualLayer, true));
    }

    private DisplacementData? GetMarkingDisplacement(
        VisualOrganMarkingsComponent markings,
        HumanoidVisualLayers layer,
        string? bodyTypeVisualKey,
        Sex sex)
    {
        if (bodyTypeVisualKey is not null &&
            markings.BodyTypeSexMarkingsDisplacement.TryGetValue(bodyTypeVisualKey, out var sexMap) &&
            sexMap.TryGetValue(sex, out var sexLayerMap) &&
            sexLayerMap.TryGetValue(layer, out var bodyTypeSexDisplacement))
        {
            return bodyTypeSexDisplacement;
        }

        if (markings.SexMarkingsDisplacement.TryGetValue(sex, out var layerMap) &&
            layerMap.TryGetValue(layer, out var sexDisplacement))
        {
            return sexDisplacement;
        }

        if (bodyTypeVisualKey is not null &&
            markings.BodyTypeMarkingsDisplacement.TryGetValue(bodyTypeVisualKey, out var bodyTypeLayerMap) &&
            bodyTypeLayerMap.TryGetValue(layer, out var bodyTypeDisplacement))
        {
            return bodyTypeDisplacement;
        }

        return markings.MarkingsDisplacement.GetValueOrDefault(layer);
    }
}
