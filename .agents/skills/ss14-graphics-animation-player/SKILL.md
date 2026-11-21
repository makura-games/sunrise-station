---
name: ss14-graphics-animation-player
description: A deep practical guide to entity animations using the AnimationPlayerSystem in SS14: lifecycle, API, track types, keyframes/interpolation/easing, completion events, patterns and anti-patterns for production code.
---

# Animations via AnimationPlayer in SS14

This skill only covers entity animations via `AnimationPlayerSystem` :)
UI animations (`Control.PlayAnimation`) and shader/overlay effects are handled in separate skills.

## When to use

Choose `AnimationPlayerSystem` when needed:

- smoothly change the properties of components over time (`SpriteComponent`, `TransformComponent`, `PointLightComponent`, etc.);
- run flick animations of RSI states by layers;
- synchronize visual and sound in one timeline;
- handle completion/interruption of animation through events.

Don't use it for static visual states without time: this is the normal `SpriteSystem`/`GenericVisualizer` zone.

## Mental model

1. You create `Animation` (`Length` + list `AnimationTracks`).
2. You play it through `AnimationPlayerSystem.Play(...)` under the unique `key`.
3. The system moves the playback every frame and applies the track values.
4. Upon completion, `AnimationCompletedEvent` (`Finished = true`) arrives.
5. When manually stopping `Stop(...)` also comes `AnimationCompletedEvent`, but `Finished = false`.

Idea: animation is a local timeline of the effect, not network business logic :)

## Important client/server boundary

- `AnimationPlayerComponent` — client entity of the visual.
- On the server, this component is ignored by the component factory.
- The server reports the state, the client decides when and how to visually animate.

## API parsing `AnimationPlayerSystem`

### Launch

- `Play(EntityUid uid, Animation animation, string key)`
- `Play(Entity<AnimationPlayerComponent> ent, Animation animation, string key)`

Practice:

- use stable key constants;
- before `Play` check `HasRunningAnimation` if the key may be repeated.

### Checking running

- `HasRunningAnimation(EntityUid uid, string key)`
- `HasRunningAnimation(EntityUid uid, AnimationPlayerComponent? component, string key)`
- `HasRunningAnimation(AnimationPlayerComponent component, string key)`

### Stop

- `Stop(Entity<AnimationPlayerComponent?> entity, string key)`
- `Stop(EntityUid uid, AnimationPlayerComponent? component, string key)`

The stop triggers a completion event with `Finished = false`.

### Events

- `AnimationStartedEvent`
- `AnimationCompletedEvent`
  - `Key`
  - `Finished` (natural termination or forced)

## Structure `Animation`

- `Length`: total playback length.
- `AnimationTracks`: several tracks run synchronously.

Rule: `Length` must cover all the necessary key phases of the tracks.

## Types of tracks and when to take which one

### `AnimationTrackComponentProperty`

Changes the component property by keyframes.

Required fields:

- `ComponentType = typeof(...)`
- `Property = nameof(...)`
- `KeyFrames`
- optional `InterpolationMode`

Use for:

- `SpriteComponent.Scale/Offset/Color/Rotation`;
- `TransformComponent.LocalPosition`;
- `PointLightComponent.AnimatedRadius/AnimatedEnable/Rotation`, etc.

### `AnimationTrackSpriteFlick`

Controls the RSI-state of the layer over time.

Required fields:

- `LayerKey` (usually layer enum);
- `KeyFrames` with `StateId`.

Use it when you need to play the state animation of the layer.

### `AnimationTrackPlaySound`

Plays sound on the given keyframes.

Use for precise synchronization of “visual + sound” in one timeline.

## KeyFrame and interpolation

`AnimationTrackProperty.KeyFrame(value, keyTime, easing?)`:

- `keyTime` is a **delta** from the previous keyframe, not an absolute time.
- `easing` applies to the transition between the previous and current frame.

`AnimationInterpolationMode`:

- `Linear`
- `Cubic`
- `Nearest`
- `Previous`

For unsupported types, interpolation actually behaves like a discrete step (previous value).

## Practical examples

### Example 1: secure launch by key

```csharp
private const string AnimKey = "rotating_light";

private void TryPlayRotation(EntityUid uid, AnimationPlayerComponent player, Animation anim)
{
    // We do not allow key conflicts in PlayingAnimations.
    if (_anim.HasRunningAnimation(uid, player, AnimKey))
        return;

    _anim.Play((uid, player), anim, AnimKey);
}
```

### Example 2: correct stop + cleanup

```csharp
private void StopFallAnimation(EntityUid uid, AnimationPlayerComponent player, SpriteComponent sprite, Vector2 originalScale)
{
    // First, we return the visual parameters to their basic state.
    _sprite.SetScale((uid, sprite), originalScale);

    // Then we stop the animation by key.
    _anim.Stop((uid, player), "chasm_fall");
}
```

### Example 3: property-track with easing

```csharp
private static Animation BuildPickupAnim(Vector2 from, Vector2 to, Color startColor)
{
    return new Animation
    {
        Length = TimeSpan.FromMilliseconds(175),
        AnimationTracks =
        {
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(TransformComponent),
                Property = nameof(TransformComponent.LocalPosition),
                InterpolationMode = AnimationInterpolationMode.Linear,
                KeyFrames =
                {
                    // keyTime - delta from the previous keyframe.
                    new AnimationTrackProperty.KeyFrame(from, 0f),
                    new AnimationTrackProperty.KeyFrame(to, 0.175f, Easings.OutQuad)
                }
            },
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Color),
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(startColor, 0f),
                    new AnimationTrackProperty.KeyFrame(startColor.WithAlpha(0f), 0.175f, Easings.OutQuad)
                }
            }
        }
    };
}
```

### Example 4: combined track (flick + light)

```csharp
private static readonly Animation ProximityAnim = new()
{
    Length = TimeSpan.FromSeconds(0.6f),
    AnimationTracks =
    {
        new AnimationTrackSpriteFlick
        {
            LayerKey = ProximityTriggerVisualLayers.Base,
            KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame("flashing", 0f) }
        },
        new AnimationTrackComponentProperty
        {
            ComponentType = typeof(PointLightComponent),
            Property = nameof(PointLightComponent.AnimatedRadius),
            InterpolationMode = AnimationInterpolationMode.Nearest,
            KeyFrames =
            {
                new AnimationTrackProperty.KeyFrame(0.1f, 0f),
                new AnimationTrackProperty.KeyFrame(3f, 0.1f),
                new AnimationTrackProperty.KeyFrame(0.1f, 0.5f)
            }
        }
    }
};
```

### Example 5: manual loop via `AnimationCompletedEvent`

```csharp
private void OnAnimationCompleted(EntityUid uid, RotatingLightComponent comp, AnimationCompletedEvent args)
{
    if (args.Key != "rotating_light")
        return;

    // We re-beat only at natural completion.
    if (!args.Finished)
        return;

    if (!TryComp<AnimationPlayerComponent>(uid, out var player))
        return;

    _anim.Play((uid, player), BuildRotation(comp.Speed), "rotating_light");
}
```

### Example 6: flick with infinite length + sound

```csharp
private Animation BuildPrimingAnimation(ResolvedSoundSpecifier? sound)
{
    var anim = new Animation
    {
        Length = TimeSpan.MaxValue,
        AnimationTracks =
        {
            new AnimationTrackSpriteFlick
            {
                LayerKey = TriggerVisualLayers.Base,
                KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame("primed", 0f) }
            }
        }
    };

    if (sound != null)
    {
        anim.AnimationTracks.Add(new AnimationTrackPlaySound
        {
            KeyFrames = { new AnimationTrackPlaySound.KeyFrame(sound.Value, 0f) }
        });
    }

    return anim;
}
```

### Example 7: Animate a color without duplicating a component

```csharp
private Animation BuildColorFlash(Color from, Color to, float seconds)
{
    return new Animation
    {
        Length = TimeSpan.FromSeconds(seconds),
        AnimationTracks =
        {
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Color),
                InterpolationMode = AnimationInterpolationMode.Linear,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(from, 0f),
                    new AnimationTrackProperty.KeyFrame(to, seconds)
                }
            }
        }
    };
}
```

### Example 8: connection with appearance without spam

```csharp
private void UpdateTriggeredState(EntityUid uid, AnimationPlayerComponent player, ProximityTriggerVisuals state)
{
    switch (state)
    {
        case ProximityTriggerVisuals.Active:
            if (!_anim.HasRunningAnimation(uid, player, "proximity"))
                _anim.Play((uid, player), ProximityAnim, "proximity");
            break;
        case ProximityTriggerVisuals.Inactive:
        case ProximityTriggerVisuals.Off:
            _anim.Stop((uid, player), "proximity");
            break;
    }
}
```

## Patterns 🙂

- Store animation keys as constants near the system.
- Before `Play`, check `HasRunningAnimation` if the key may be repeated.
- For replays, prefer `AnimationCompletedEvent` + `args.Finished`.
- On `ComponentShutdown/Remove` return the original visual values ​​(scale/offset/color).
- Combine several tracks in one `Animation` if they need a common timeline.
- Use `nameof(...)` for `Property` to avoid breaking during refactoring.

## Anti-patterns ❌

- Play `Play` again with the same key without checking (risk of exclusion for a duplicate key).
- Forget to cleanup after an interrupted animation.
- Try to animate properties that are not animable.
- Embed server business logic into the client animation system.
- Re-create heavy animations every frame without having to.
- Confuse entity-animation (`AnimationPlayerSystem`) and UI-animation (`Control.PlayAnimation`).

## Checklist before change ✅

- Is there a stable `AnimationKey`?
- Is there a guard for repeated `Play`?
- Is `Finished` checked in `AnimationCompletedEvent`?
- Are the original visual parameters returned during shutdown/stop?
- Does `Length` really cover all keyframe transitions?
- Is the correct `ComponentType` + `Property` specified for `AnimationTrackComponentProperty`?

## Common errors

- `keyTime` is treated as an absolute timestamp, not a delta.
- The animation is stopped, but they forget to return the original `SpriteComponent` state.
- Use `string` literals instead of `nameof(...)` and break the animation when renaming.
- Listen to `AnimationCompletedEvent`, but do not filter `args.Key`.
- Launch animation on an entity without the required component.
- They don’t differentiate between `Finished = true` and `Finished = false`, which causes the loop to start in the wrong place ⚠
