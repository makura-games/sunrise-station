using Content.Shared._Sunrise.Light.Visualizers;
using Content.Shared.Light;
using Robust.Client.Animations;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Animations;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Sunrise.Light.Visualizers;

public sealed partial class SunrisePoweredLightSparksSystem : VisualizerSystem<SunrisePoweredLightSparksComponent>
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityQuery<MetaDataComponent> _metaQuery = default!;

    private const string FlickerAnimationKey = "sunrise-powered-light-flicker";
    private const string OnLayer = "sunrisePoweredLightOn";
    public const string DefaultLayer = "sunrisePoweredLightSparks";

    public override void Initialize()
    {
        base.Initialize();
        // Сначала применяем новое состояние лампы, затем запускаем и проигрываем локальные вспышки.
        UpdatesAfter.Add(typeof(AppearanceSystem));
        UpdatesBefore.Add(typeof(AnimationPlayerSystem));
        SubscribeLocalEvent<SunrisePoweredLightSparksComponent, AnimationCompletedEvent>(OnAnimationCompleted);
        SubscribeLocalEvent<SunrisePoweredLightSparksComponent, ComponentShutdown>(OnShutdown);
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

        RestoreLight(ent);
        HideFlickerLayers(ent, ent.Comp);
    }

    private void OnShutdown(Entity<SunrisePoweredLightSparksComponent> ent, ref ComponentShutdown args)
    {
        StopFlicker(ent, ent.Comp);
        RemComp<ActiveSunrisePoweredLightSparksComponent>(ent);
        RemComp<SunrisePoweredLightSparksVisualsComponent>(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<ActiveSunrisePoweredLightSparksComponent, SunrisePoweredLightSparksComponent, SpriteComponent, PointLightComponent>();
        while (query.MoveNext(out var uid, out var active, out var sparks, out var sprite, out var light))
        {
            if (_timing.CurTime < active.NextFlickerTime)
                continue;

            if (sparks.States.Count == 0)
                continue;

            if ((_metaQuery.GetComponent(uid).Flags & MetaDataFlags.Detached) != 0)
                continue;

            // Stop вызывает AnimationCompletedEvent, поэтому старую анимацию завершаем до установки новых слоёв.
            StopFlicker(uid, sparks);
            UpdateLayer((uid, sprite), sparks.Layer, _random.Pick(sparks.States), sparks.SparkSprite);
            if (sparks.SparkStates.Count > 0 && _random.Prob(sparks.SparksChance))
                UpdateLayer((uid, sprite), sparks.SparksLayer, _random.Pick(sparks.SparkStates), sparks.SparkSprite);

            PlayFlicker(uid, sparks, light);
            active.NextFlickerTime = GetNextFlickerTime(sparks);
        }
    }

    private void UpdateVisuals(
        EntityUid uid,
        SunrisePoweredLightSparksComponent component,
        SpriteComponent spriteComp,
        AppearanceComponent? appearance)
    {
        var sprite = (uid, spriteComp);
        var visuals = EnsureComp<SunrisePoweredLightSparksVisualsComponent>(uid);
        AppearanceSystem.TryGetData<PoweredLightState>(uid, PoweredLightVisuals.BulbState, out var state, appearance);
        AppearanceSystem.TryGetData<bool>(uid, SunrisePoweredLightVisuals.HasPower, out var hasPower, appearance);

        UpdateLayer(sprite, OnLayer, state == PoweredLightState.On && hasPower ? "base" : null, null);

        if (state != PoweredLightState.Broken)
        {
            visuals.ShouldFlicker = null;
            RemComp<ActiveSunrisePoweredLightSparksComponent>(uid);
            StopFlicker(uid, component);
            return;
        }

        if (!hasPower || component.States.Count == 0 || !HasComp<PointLightComponent>(uid))
        {
            RemComp<ActiveSunrisePoweredLightSparksComponent>(uid);
            StopFlicker(uid, component);
            return;
        }

        visuals.ShouldFlicker ??= _random.Prob(component.FlickerChance);
        if (visuals.ShouldFlicker != true)
            return;

        if (HasComp<ActiveSunrisePoweredLightSparksComponent>(uid))
            return;

        var active = AddComp<ActiveSunrisePoweredLightSparksComponent>(uid);
        active.NextFlickerTime = GetNextFlickerTime(component);
    }

    private void PlayFlicker(EntityUid uid, SunrisePoweredLightSparksComponent component, PointLightComponent light)
    {
        var player = EnsureComp<AnimationPlayerComponent>(uid);
        var visuals = EnsureComp<SunrisePoweredLightSparksVisualsComponent>(uid);
        visuals.OriginalLightEnergy = light.Energy;
        _animation.Play((uid, player), BuildFlickerAnimation(component, light), FlickerAnimationKey);
        visuals.FlickerAudio = _audio.PlayEntity(component.FlickerSound, Filter.Local(), uid, false)?.Entity;
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

        RestoreLight(uid);
        if (TryComp<SunrisePoweredLightSparksVisualsComponent>(uid, out var visuals))
            visuals.FlickerAudio = _audio.Stop(visuals.FlickerAudio);

        HideFlickerLayers(uid, component);
    }

    private void RestoreLight(EntityUid uid)
    {
        if (!TryComp<SunrisePoweredLightSparksVisualsComponent>(uid, out var visuals) ||
            visuals.OriginalLightEnergy is not { } energy)
            return;

        if (TryComp<PointLightComponent>(uid, out var light))
        {
            // При замене лампы сервер мог уже включить исправный светильник.
            var enabled = AppearanceSystem.TryGetData<PoweredLightState>(uid, PoweredLightVisuals.BulbState, out var state) &&
                state == PoweredLightState.On;
            if (!enabled)
                _pointLight.SetEnergy(uid, energy, light);
            _pointLight.SetEnabled(uid, enabled, light);
        }

        visuals.OriginalLightEnergy = null;
    }

    private TimeSpan GetNextFlickerTime(SunrisePoweredLightSparksComponent component)
    {
        var min = Math.Max(0, component.MinFlickerDelay.TotalSeconds);
        var max = Math.Max(min, component.MaxFlickerDelay.TotalSeconds);
        return _timing.CurTime + TimeSpan.FromSeconds(min + (max - min) * _random.NextDouble());
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

        if (!TrySetState(layer, state, sprite.Comp.BaseRSI, fallbackRsi))
        {
            SpriteSystem.LayerSetVisible(layer, false);
            SpriteSystem.LayerSetAutoAnimated(layer, false);
            return;
        }

        SpriteSystem.LayerSetVisible(layer, true);
        SpriteSystem.LayerSetAutoAnimated(layer, true);
    }

    private bool TrySetState(Layer layer, string state, RSI? baseRsi, ResPath? fallbackRsi)
    {
        // У напольных, абдукторских и праздничных светильников есть собственные состояния искр.
        if (baseRsi?.TryGetState(state, out _) == true)
        {
            SpriteSystem.LayerSetRsi(layer, (RSI?) null, state);
            return true;
        }

        if (fallbackRsi != null)
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
