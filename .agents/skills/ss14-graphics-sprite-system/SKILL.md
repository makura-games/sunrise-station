---
name: ss14-graphics-sprite-system
description: An in-depth practical guide to SpriteSystem in Space Station 14: lifecycle, full API for groups of methods, working with layers and layer-map, practical patterns and anti-patterns. Use it when developing dynamic sprites, visualizer systems and refactoring outdated SpriteComponent calls.
---

#SpriteSystem in SS14

This skill only covers `SpriteSystem` and the practice of its application in the current SS14 architecture :)
Related topics (`GenericVisualizer`, `Appearance`, overlays, shaders, UI) are covered in separate skills.

## When to use

Use `SpriteSystem` when you need:

- change the appearance of an entity in runtime on the client;
- manage layers, their visibility, color, offset, RSI/texture;
- dynamically add/remove layers;
- use layer map for stable addressing of layers by key;
- make accurate visual updates from visualizer systems.

Don’t bring logic here that can only be prototyped (static `Sprite` without dynamics), and don’t mix `SpriteSystem` with network business logic.

## Source of truth

- Consider outdated examples on direct calls to `SpriteComponent` historical.

## SpriteSystem mental model

1. The server and shared layer decide the state of the gameplay.
2. The client receives state data (often via an appearance/visualizer).
3. The client system calls `SpriteSystem` and changes only the visual appearance.
4. The sprite is rendered from a set of layers (`Layer`) with their parameters.
5. Layer map gives stable keys (`Enum`/`string`) instead of "magic" indexes.

Idea: the visual changes locally, deterministically and cheaply over the network :)

## API parsing (by groups)

### 1) Entity-level setters

Use for properties of the entire sprite:

- `SetScale`, `SetRotation`, `SetOffset`
- `SetVisible`, `SetDrawDepth`, `SetColor`
- `SetBaseRsi`, `SetContainerOccluded`
- `SetSnapCardinals`, `SetGranularLayersRendering`

### 2) Layer CRUD

Creating/deleting layers:

- `AddBlankLayer`
- `AddLayer` (from `Layer`, `SpriteSpecifier`, `PrototypeLayerData`)
- `AddRsiLayer`
- `AddTextureLayer`
- `RemoveLayer`
- `TryGetLayer`
- `LayerExists`

### 3) Layer map API

Key mapping operations:

- `LayerMapSet`, `LayerMapAdd`
- `LayerMapRemove`
- `LayerMapTryGet`, `LayerMapGet`
- `LayerMapReserve`

The `Enum` and `string` keys are supported. For project code, usually choose `Enum`.

### 4) Layer mutators (main working set)

- `LayerSetData`
- `LayerSetSprite`
- `LayerSetTexture`
- `LayerSetRsiState`
- `LayerSetRsi`
- `LayerSetScale`
- `LayerSetRotation`
- `LayerSetOffset`
- `LayerSetVisible`
- `LayerSetColor`
- `LayerSetDirOffset`
- `LayerSetAnimationTime`
- `LayerSetAutoAnimated`
- `LayerSetRenderingStrategy`

### 5) Layer getters

- `LayerGetRsiState`
- `LayerGetEffectiveRsi`
- `LayerGetDirections`
- `LayerGetDirectionCount`

### 6) Bounds / Render / Helpers

- `GetLocalBounds` (for the entire sprite and a separate layer)
- `CalculateBounds`
- `RenderSprite`
- `GetFrame`
- `Frame0`, `RsiStateLike`
- `GetIcon`, `GetPrototypeIcon`, `GetPrototypeTextures`
- `GetFallbackState`, `GetFallbackTexture`
- `GetState`, `GetTexture`
- `GetSpriteWorldPosition`, `GetSpriteScreenCoordinates`

### 7) Utility methods

- `ForceUpdate`
- `SetAutoAnimateSync` (for sprite/layer)
- `CopySprite`
- `QueueUpdateIsInert` / `QueueUpdateInert`

### Overload pattern

Most layer methods have variants:

- by index `int`;
- by key `Enum`;
- by key `string`;
- by object `Layer`.

Rule: in gameplay code, `Enum` keys via layer map are preferable.

## Must know about obsolete wrappers

`SpriteComponent` has many deprecated proxy methods (`[Obsolete]`) that redirect to `SpriteSystem`.

Why is direct calls to `SpriteComponent` an anti-pattern:

- blurring the unified update API;
- complicate refactoring and auditing of visual changes;
- break consistency with the modern ECS style in the project;
- increase the risk of silent regressions when changing the engine ⚠

Briefly: new code is written through `SpriteSystem`, not through the old component methods.

## Practical examples

### Example 1: Basic entity sprite setup

```csharp
public void ApplyMachineLook(EntityUid uid, SpriteComponent sprite, bool highlighted)
{
    // We change only the visual properties of the entity.
    _sprite.SetVisible((uid, sprite), true);
    _sprite.SetDrawDepth((uid, sprite), (int)DrawDepth.Machines);
    _sprite.SetColor((uid, sprite), highlighted ? Color.Cyan : Color.White);
}
```

### Example 2: layer reserve by enum key and data filling

```csharp
private enum MachineLayerKey : byte
{
    Base,
    Status
}

public void SetStatusLayer(EntityUid uid, SpriteComponent sprite, PrototypeLayerData data)
{
    // We guarantee the presence of a layer under the key.
    var index = _sprite.LayerMapReserve((uid, sprite), MachineLayerKey.Status);

    // We update the entire layer via PrototypeLayerData.
    _sprite.LayerSetData((uid, sprite), index, data);
    _sprite.LayerSetVisible((uid, sprite), index, true);
}
```

### Example 3: Safely deleting a layer by key

```csharp
public void HideStatusLayer(EntityUid uid, SpriteComponent sprite)
{
    // We don’t consider the absence of a layer an error: this is a normal idempotent flow.
    _sprite.RemoveLayer((uid, sprite), MachineLayerKey.Status, logMissing: false);
}
```

### Example 4: Dynamically generating and clearing text layers

```csharp
public void RebuildTextLayers(EntityUid uid, SpriteComponent sprite, IReadOnlyList<int> oldLayers, string text)
{
    // First we clear the old temporary layers.
    foreach (var old in oldLayers)
        _sprite.RemoveLayer((uid, sprite), old, logMissing: false);

    var x = 0f;
    foreach (var ch in text)
    {
        // For each symbol we create a separate layer.
        var layer = _sprite.AddRsiLayer((uid, sprite), new RSI.StateId(ch.ToString()), _fontRsi);
        _sprite.LayerSetOffset((uid, sprite), layer, new Vector2(x, 0f));
        _sprite.LayerSetVisible((uid, sprite), layer, true);

        x += 0.5f; // Step between characters.
    }
}
```

### Example 5: Directional layer offsets

```csharp
public void ApplyDirectionalOffset(EntityUid uid, SpriteComponent sprite, Enum pipeLayerKey, DirectionOffset dirOffset)
{
    // The same layer looks different in different directions.
    _sprite.LayerSetDirOffset((uid, sprite), pipeLayerKey, dirOffset);
}
```

### Example 6: Bit mask for a group of indicator layers

```csharp
[Flags]
public enum IndicatorBits : byte
{
    None = 0,
    Powered = 1 << 0,
    Charging = 1 << 1,
    Broken = 1 << 2
}

public void UpdateIndicators(EntityUid uid, SpriteComponent sprite, IndicatorBits bits)
{
    // Each flag controls its own layer, without long if-chains for states.
    _sprite.LayerSetVisible((uid, sprite), "powered",  (bits & IndicatorBits.Powered)  != 0);
    _sprite.LayerSetVisible((uid, sprite), "charging", (bits & IndicatorBits.Charging) != 0);
    _sprite.LayerSetVisible((uid, sprite), "broken",   (bits & IndicatorBits.Broken)   != 0);
}
```

### Example 7: replacing base RSI + draw depth in one update

```csharp
public void ApplyStorageVisualMode(EntityUid uid, SpriteComponent sprite, bool opened)
{
    // We change the base RSI and rendering depth as one logical update.
    _sprite.SetBaseRsi((uid, sprite), opened ? _openedRsi : _closedRsi);
    _sprite.SetDrawDepth((uid, sprite), opened ? (int)DrawDepth.SmallObjects : (int)DrawDepth.Objects);
    _sprite.ForceUpdate(uid); // Pushing for an immediate visual update.
}
```

## Patterns 🙂

- Use `LayerMapReserve` + `Enum` keys for stable addressing of layers.
- Think of a layer as a minimal unit of visual state, not "the whole sprite at once."
- Group changes to one visual event in one method.
- For temporary layers, always keep an explicit cleaning cycle.
- For complex indicators, use bit masks rather than cascades of Boolean fields.
- For identical visual entities, copy the layer configuration via `CopySprite`.

## Anti-patterns ❌

- Direct deprecated calls to `SpriteComponent` instead of `SpriteSystem`.
- Working with “magic indexes” of layers without a layer map.
- Mixing gameplay logic and visual update in one method.
- Frequent complete rebuilds of all layers when changing one flag.
- Using `string` keys where `Enum` already exists.
- No processing of `RemoveLayer(..., logMissing: false)` in idempotent threads.

## Checklist before change ✅

- Do you change the visual through `SpriteSystem`, and not through the obsolete API?
- Key layers are available via `Enum`/layer map?
- Is there symmetrical clearing for dynamic layers?
- Do local updates not rebuild the entire sprite for no reason?
- Is the appropriate method selected: `LayerSetData` (bulk) or point mutator?
- The changes do not introduce network business logic into the client visual code?

## Common errors

- The layer is in the prototype, but is not reserved/linked to the expected key.
- The state value was updated, but the desired layer remained invisible.
- Incorrect overload was used (index instead of key, or vice versa).
- Temporary layers are not cleared and accumulate between updates.
- Incorrect `DrawDepth` causes the object to “disappear” behind neighboring entities.
- Trying to treat an architectural problem with `ForceUpdate` rather than with the correct update flow.
