---
name: ss14-graphics-overlays
description: An in-depth practical guide to the SS14 overlay architecture: OverlaySpace, lifecycle, communication with shaders, ScreenTexture, render targets, stencil composition, graphics primitives and render optimization.
---

# Overlays and graphics pipeline SS14

This skill only covers overlays: their lifecycle, render passes, resource caching, stencil and primitives :)
For details of the shader language and built-in functions, see the separate shader-skill.

## How overlays are actually rendered

1. The manager stores one instance of overlay per type and sorts them by `ZIndex`.
2. For each overlay, `BeforeDraw(...)` is called.
3. If `RequestScreenTexture == true`, the engine copies the current framebuffer to `ScreenTexture` (expensive).
4. If `OverwriteTargetFrameBuffer == true`, the target buffer is cleared before drawing.
5. Call `Draw(...)` in the corresponding space (`ScreenSpace`/`WorldSpace`, etc.).

Conclusion:
- Keep the early exit logic in `BeforeDraw` so as not to trigger unnecessary screen copying.
- `Draw` should be as short and deterministic as possible.

## Select OverlaySpace

- `ScreenSpace`: UI-like effects on top of the world.
- `ScreenSpaceBelowWorld`: UI effects under the world.
- `WorldSpace`: effects over the world, often fullscreen by `WorldBounds`.
- `WorldSpaceBelowFOV`: effects under the final FOV.
- `WorldSpaceEntities`: effects on the same layer as entities.
- `BeforeLighting`: special passes before applying the light buffer.

## Basic shader-overlay template

```csharp
public sealed class ExampleOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // Early exit: we do not render the effect without the required state.
        return _effectStrength > 0f && args.Viewport.Eye == _playerEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("Strength", _effectStrength);

        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawRect(args.WorldBounds, Color.White); // fullscreen pass
        args.WorldHandle.UseShader(null); // be sure to reset shader-state
    }
}
```

## Multi-pass via render target (blur/compose)

```csharp
protected override bool BeforeDraw(in OverlayDrawArgs args)
{
    var size = (Vector2i) (args.Viewport.Size * _downscale);
    var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());

    // We create/recreate RT only when the viewport size changes.
    if (res.PassA == null || res.PassA.Size != size)
    {
        res.PassA?.Dispose();
        res.PassB?.Dispose();
        res.PassA = _clyde.CreateRenderTarget(size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb));
        res.PassB = _clyde.CreateRenderTarget(size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb));
    }

    return true;
}

protected override void Draw(in OverlayDrawArgs args)
{
    if (ScreenTexture == null)
        return;

    var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());
    var bounds = new Box2(Vector2.Zero, res.PassA!.Size);

    // Pass 1
    args.WorldHandle.RenderInRenderTarget(res.PassA, () =>
    {
        _blurX.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        args.WorldHandle.UseShader(_blurX);
        args.WorldHandle.DrawRect(bounds, Color.White);
    }, Color.Transparent);

    // Pass 2
    args.WorldHandle.RenderInRenderTarget(res.PassB!, () =>
    {
        _blurY.SetParameter("SCREEN_TEXTURE", res.PassA.Texture);
        args.WorldHandle.UseShader(_blurY);
        args.WorldHandle.DrawRect(bounds, Color.White);
    }, Color.Transparent);

    // Composite
    _final.SetParameter("SCREEN_TEXTURE", ScreenTexture);
    _final.SetParameter("BLURRED_TEXTURE", res.PassB.Texture);
    args.WorldHandle.UseShader(_final);
    args.WorldHandle.DrawRect(args.WorldBounds, Color.White);
    args.WorldHandle.UseShader(null);
}
```

## Stencil composition: mask + rendering

```csharp
// 1) Write the mask to the intermediate RT.
handle.RenderInRenderTarget(stencilRt, () =>
{
    handle.SetTransform(localMatrix);
    handle.DrawRect(maskRegion, Color.White); // area where the effect should take effect
}, Color.Transparent);

// 2) Apply stencil-mask shader.
handle.UseShader(_proto.Index(stencilMaskShader).Instance());
handle.DrawTextureRect(stencilRt.Texture, worldBounds);

// 3) Draw the final layer only in the allowed stencil area.
handle.UseShader(_proto.Index(stencilDrawShader).Instance());
handle.DrawTextureRect(effectRt.Texture, worldBounds);
handle.UseShader(null);
```

## Primitives for visualization and tactics

```csharp
// Direction line.
args.WorldHandle.DrawLine(start, end, Color.Aqua);

// Circle at destination.
args.WorldHandle.DrawCircle(end, 0.4f, Color.Red, false);
```

Use this for:
- tactical indicators;
- debugging vectors;
- guides/zone boundaries.

## Patterns 😎

- Check `args.Viewport.Eye` against the local player's eye before expensive rendering.
- Keep the expensive condition in `BeforeDraw`, not in the middle of `Draw`.
- Cache per-viewport resources via `OverlayResourceCache<T>`.
- Recreate RT only for resize or format change.
- Always reset the graphical state: `UseShader(null)` and, if necessary, `SetTransform(Matrix3x2.Identity)`.
- Control the order of effects through `ZIndex`, not through the “random” registration order.

## Anti-patterns

- Request `ScreenTexture` if the shader doesn't read it.
- Create a render target every frame without cache.
- Leave shader/transform active after `Draw` and break subsequent overlay passes.
- Rely on the same `ZIndex` for strict stacking order.
- Use `WorldAABB` for a fullscreen effect where you need a correctly rotated `WorldBounds` ⚠️

## Mini checklist before PR

- The overlay has a clear `OverlaySpace`.
- Expensive checks are moved to `BeforeDraw`.
- All temporary RTs are released via `Dispose`.
- The render state after `Draw` returns to neutral.
- Behavior tested on several viewport scale/zoom values.
