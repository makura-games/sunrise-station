---
name: ss14-graphics-shaders
description: An in-depth practical guide to SS14 and SWSL shaders: syntax, presets, built-in variables/functions, parameters, debugging and architectural solutions. Use it for tasks about shader prototype, uniform, light_mode/blend_mode, stencil, compatibility and GPU effects.
---

# SS14 shaders (SWSL)

This skill covers only the shader part: language, runtime, built-in functions, parameters and debugging :)
If the task is about the lifecycle of overlays, `OverlaySpace`, render target and primitives, use a separate skill about overlays.

## Short decision tree

1. Do you only need blending/lighting/stencil mode without custom math?
- Yes: select `kind: canvas`.
- No: choose `kind: source`.

2. Need a custom vertex (deformation, separate varyings)?
- Yes: add `vertex()`.
- No: `fragment()` is enough.

3. Need full control over vertex transformation without auto-lighting?
- Yes: `preset raw`.
- No: `preset default`.

## Execution model

1. The shader prototype defines `kind` and default parameters.
2. SWSL is parsed into `uniform`/`varying`/`const`/functions + include dependencies.
3. The engine wraps the code in a `default` or `raw` template and adds a common library of functions.
4. During rendering, `ShaderInstance` receives parameter values ​​from C# and is applied to the draw-call.

## SWSL language: what is supported

- Top level directives: `light_mode`, `blend_mode`, `preset`.
- Announcements: `uniform`, `varying`, `const`.
- Functions: regular helper functions + special entrypoint functions `vertex()` and `fragment()`.
- Function parameters: `in`, `out`, `inout` are supported.
- Preprocessor: `#include`, `#ifdef`, `#ifndef`, `#else`, `#endif`.

Critical:
- For numeric types (`float`, `int`, `vec*`, `mat*`) set the qualifier (`lowp`/`mediump`/`highp`), otherwise you will get instability between GPUs.
- Arrays are not supported for all types. Safe zone: `float[]`, `vec2[]`, `vec4[]`, `bool[]`.

## Built-in variables and functions

Available common uniforms:
- `TIME`
- `SCREEN_PIXEL_SIZE`
- `TEXTURE`
- `TEXTURE_PIXEL_SIZE`
- `projectionMatrix`
- `viewMatrix`

Frequently used functions:
- `zTexture(uv)` and `zTextureSpec(tex, uv)` for correct sampling.
- `zAdjustResult(col)` for correct final color output.
- `zFromSrgb(col)` / `zToSrgb(col)` for color space conversions.
- `zGrayscale(...)`, `zGrayscale_BT709(...)`, `zGrayscale_BT601(...)`.
- `zRandom(...)`, `zNoise(...)`, `zFBM(...)`.
- `zCircleGradient(...)`.
- `zClydeShadowDepthPack(...)` / `zClydeShadowDepthUnpack(...)`.

Additionally in `preset raw`:
- `apply_mvp(vertex)` and `pixel_snap(vertex)` for the vertex stage.

## Example A: basic fullscreen fragment

```glsl
uniform sampler2D SCREEN_TEXTURE;

void fragment() {
    // We take the current screen taking into account the engine selection rules.
    highp vec4 src = zTextureSpec(SCREEN_TEXTURE, UV);

    // We convert the image to grayscale using the engine function.
    highp float gray = zGrayscale(src.rgb);

    // We return the result with the original alpha.
    COLOR = vec4(vec3(gray), src.a);
}
```

## Example B: vertex deformation via varying

```glsl
uniform sampler2D displacementMap;
uniform highp float displacementSize;
uniform highp vec4 displacementUV;

varying highp vec2 displacementUVOut;

void vertex() {
    // We pass the UV for the displacement map from the vertex to the fragment.
    displacementUVOut = mix(displacementUV.xy, displacementUV.zw, tCoord2);
}

void fragment() {
    // We read the displacement map and shift the sampling of the main texture.
    highp vec4 disp = texture2D(displacementMap, displacementUVOut);
    highp vec2 offset = (disp.xy - vec2(128.0 / 255.0)) / (1.0 - 128.0 / 255.0);
    COLOR = zTexture(UV + offset * TEXTURE_PIXEL_SIZE * displacementSize * vec2(1.0, -1.0));
    COLOR.a *= disp.a; // The displacement map alpha works like a mask.
}
```

## Example C: working correctly with ShaderInstance in C#

```csharp
// We take a unique instance to safely change uniforms.
var shader = prototype.Index(shaderId).InstanceUnique();

// We set the effect parameters every frame/tick as needed.
shader.SetParameter("Strength", strength);
shader.SetParameter("SCREEN_TEXTURE", screenTexture);

handle.UseShader(shader);
handle.DrawRect(bounds, Color.White); // A real draw-call where the shader is applied.
handle.UseShader(null);               // Explicit state reset.
```

## Patterns 🙂

- Use `InstanceUnique()` for mutable parameters.
- Make heavy parameters (`strength`, `phase`, `count`) external uniforms, and do not put them in `const`.
- For noise/iterations, use lower precision where visually acceptable.
- Do early exit by alpha/mask if the pixel should not be rendered.
- Use engine helper functions (`zTexture*`, `zGrayscale`, `zCircleGradient`) instead of reinvention.
- For iterative debugging, use reloading shaders without restarting the client.

## Anti-patterns

- Change the parameters of `Instance()` (shared immutable) and receive runtime exceptions.
- Rely on deprecated doc variable names instead of the actual behavior of current templates.
- Expect any type to support arrays of uniforms.
- Write a shader without precision qualifiers and assume that it is “the same everywhere.”
- Blindly transfer old examples without checking the date and commenting on problems ⚠️

## Mini checklist before use

- The correct `kind` (`canvas`/`source`) is selected.
- The correct `preset` (`default`/`raw`) has been installed for the task.
- All important numeric types have a precision qualifier.
- Parameters that change during runtime are supplied via `SetParameter`.
- There is fallback behavior for weak/problematic graphics configurations.
