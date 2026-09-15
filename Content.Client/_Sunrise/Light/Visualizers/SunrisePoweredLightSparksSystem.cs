using Content.Shared._Sunrise.Light.Visualizers;
using Content.Shared.Light;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Animations;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Sunrise.Light.Visualizers;

public sealed class SunrisePoweredLightSparksSystem : VisualizerSystem<SunrisePoweredLightSparksComponent>
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;

    private const string FlickerAnimationKey = "sunrise-powered-light-flicker";
    private const string OnLayer = "sunrisePoweredLightOn";
    public const string DefaultLayer = "sunrisePoweredLightSparks";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SunrisePoweredLightSparksComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    protected override void OnAppearanceChange(EntityUid uid, SunrisePoweredLightSparksComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        UpdateVisuals(uid, component, args.Sprite, args.Component);
    }

    private void OnAnimationCompleted(Entity<SunrisePoweredLightSparksComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != FlickerAnimationKey)
            return;

        HideFlickerLayers(ent, ent.Comp);
    }

    private void UpdateVisuals(
        EntityUid uid,
        SunrisePoweredLightSparksComponent component,
        SpriteComponent spriteComp,
        AppearanceComponent? appearance)
    {
        var sprite = (uid, spriteComp);
        if (!AppearanceSystem.TryGetData<PoweredLightState>(uid, PoweredLightVisuals.BulbState, out var state, appearance) ||
            !AppearanceSystem.TryGetData<bool>(uid, SunrisePoweredLightVisuals.HasPower, out var hasPower, appearance) ||
            !TryComp<PointLightComponent>(uid, out var light))
        {
            StopFlicker(uid, component);
            UpdateLayer(sprite, OnLayer, null, null);
            HideFlickerLayers(sprite, component);
            return;
        }

        UpdateLayer(sprite, OnLayer, state == PoweredLightState.On && hasPower ? "base" : null, null);

        if (state != PoweredLightState.Broken || !hasPower)
        {
            StopFlicker(uid, component);
            HideFlickerLayers(sprite, component);
            return;
        }

        if (!AppearanceSystem.TryGetData<int>(uid, SunrisePoweredLightVisuals.FlickerSequence, out var sequence, appearance))
            return;

        if (sequence == component.FlickerSequence)
            return;

        if (!AppearanceSystem.TryGetData<string>(uid, SunrisePoweredLightVisuals.FlickerState, out var flickerState, appearance))
            return;

        if (!AppearanceSystem.TryGetData<bool>(uid, SunrisePoweredLightVisuals.ShowSparks, out var showSparks, appearance))
            return;

        string? sparkState = null;
        if (showSparks &&
            !AppearanceSystem.TryGetData<string>(uid, SunrisePoweredLightVisuals.SparkState, out sparkState, appearance))
            return;

        component.FlickerSequence = sequence;
        UpdateLayer(sprite, component.Layer, flickerState, component.SparkSprite);
        if (showSparks)
            UpdateLayer(sprite, component.SparksLayer, sparkState!, component.SparkSprite);
        else
            UpdateLayer(sprite, component.SparksLayer, null, component.SparkSprite);
        PlayFlicker(uid, component, light);
    }

    private void PlayFlicker(EntityUid uid, SunrisePoweredLightSparksComponent component, PointLightComponent light)
    {
        var player = EnsureComp<AnimationPlayerComponent>(uid);
        if (_animation.HasRunningAnimation(uid, player, FlickerAnimationKey))
            _animation.Stop(uid, player, FlickerAnimationKey);

        _animation.Play((uid, player), BuildFlickerAnimation(component, light), FlickerAnimationKey);
    }

    private Animation BuildFlickerAnimation(SunrisePoweredLightSparksComponent component, PointLightComponent light)
    {
        var duration = (float) component.FlickerDuration.TotalSeconds;
        var dimEnergy = light.Energy * component.FlickerLightEnergyMultiplier;

        return new Animation
        {
            Length = component.FlickerDuration,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(PointLightComponent),
                    InterpolationMode = AnimationInterpolationMode.Nearest,
                    Property = nameof(PointLightComponent.AnimatedEnable),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(true, 0f),
                        new AnimationTrackProperty.KeyFrame(false, duration),
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(PointLightComponent),
                    InterpolationMode = AnimationInterpolationMode.Nearest,
                    Property = nameof(PointLightComponent.Energy),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(dimEnergy, 0f),
                        new AnimationTrackProperty.KeyFrame(light.Energy, duration),
                    }
                }
            }
        };
    }

    private void StopFlicker(EntityUid uid, SunrisePoweredLightSparksComponent component)
    {
        if (TryComp<AnimationPlayerComponent>(uid, out var player))
            _animation.Stop(uid, player, FlickerAnimationKey);

        HideFlickerLayers(uid, component);
    }

    private void HideFlickerLayers(EntityUid uid, SunrisePoweredLightSparksComponent component)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        HideFlickerLayers((uid, sprite), component);
    }

    private void HideFlickerLayers(Entity<SpriteComponent> sprite, SunrisePoweredLightSparksComponent component)
    {
        UpdateLayer(sprite, component.Layer, null, component.SparkSprite);
        UpdateLayer(sprite, component.SparksLayer, null, component.SparkSprite);
    }

    private void UpdateLayer(Entity<SpriteComponent> sprite, string layerKey, string? state, ResPath? fallbackRsi)
    {
        if (state == null)
        {
            if (!SpriteSystem.LayerMapTryGet(sprite.AsNullable(), layerKey, out var layerIndex, false))
                return;

            SpriteSystem.LayerSetVisible(sprite.AsNullable(), layerIndex, false);
            SpriteSystem.LayerSetAutoAnimated(sprite.AsNullable(), layerIndex, false);
            return;
        }

        if (!TryGetSparkLayer(sprite, layerKey, out var layer))
            return;

        if (!TrySetState(layer, state, fallbackRsi))
        {
            SpriteSystem.LayerSetVisible(layer, false);
            SpriteSystem.LayerSetAutoAnimated(layer, false);
            return;
        }

        SpriteSystem.LayerSetVisible(layer, true);
        SpriteSystem.LayerSetAutoAnimated(layer, true);
    }

    private bool TrySetState(Layer layer, string state, ResPath? fallbackRsi)
    {
        if (layer.ActualRsi?.TryGetState(state, out _) == true)
        {
            SpriteSystem.LayerSetRsiState(layer, state);
            return true;
        }

        if (fallbackRsi == null)
            return false;

        SpriteSystem.LayerSetRsi(layer, fallbackRsi.Value);
        if (layer.ActualRsi?.TryGetState(state, out _) != true)
            return false;

        SpriteSystem.LayerSetRsiState(layer, state);
        return true;
    }

    private bool TryGetSparkLayer(Entity<SpriteComponent> sprite, string layerKey, [NotNullWhen(true)] out Layer? layer)
    {
        var layerIndex = SpriteSystem.LayerMapReserve(sprite.AsNullable(), layerKey);
        if (!SpriteSystem.TryGetLayer(sprite.AsNullable(), layerIndex, out layer, false))
            return false;

        SpriteSystem.LayerSetData(layer, new PrototypeLayerData
        {
            Shader = SpriteSystem.UnshadedId.Id,
            Visible = false,
        });

        return true;
    }

}
