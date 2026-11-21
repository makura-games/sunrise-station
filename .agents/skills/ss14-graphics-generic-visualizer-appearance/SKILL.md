---
name: ss14-graphics-generic-visualizer-appearance
description: A practical and architectural guide to the combination of AppearanceComponent, AppearanceSystem, VisualizerSystem and GenericVisualizer in SS14. Use it when designing network visual states, YAML visualizations and client visualizer systems.
---

# GenericVisualizer and Appearance in SS14

This skill only covers the pipeline `Appearance` + `VisualizerSystem` + `GenericVisualizer` :)
Low-level rendering and detailed `SpriteSystem` API can be analyzed in a separate sprite-skill.

## When to choose this skill

Choose it if the task is about:

- replication of visual state from server to client;
- designing `Appearance` keys and payload data;
- choice between `GenericVisualizer` and custom visualizer;
- YAML description of visualization via `visuals`;
- processing `AppearanceChangeEvent`.

## Source of truth and relevance

- If there is a conflict between documentation and code, the current code takes precedence.
- Documentation is used as conceptual support, but the API is verified against implementation.
- Practical recommendations and examples are based on fresh patterns from current code.

## End-to-end architecture

1. Server logic calculates the visual state.
2. The server writes data to `AppearanceComponent` through `SharedAppearanceSystem.SetData(...)`.
3. The data enters the component state and is synchronized over the network.
4. Client `AppearanceSystem` accepts state, updates the appearance data dictionary, and queues the update.
5. During `FrameUpdate`, `AppearanceChangeEvent` is raised.
6. Client visualizer systems (`VisualizerSystem<T>`) apply data to the sprite.
7. If simple mapping of values ​​on layer-data is enough, `GenericVisualizerSystem` does this.

Idea: the server conveys not “how to draw”, but “what is visually true”; the client decides “how to draw” :)

## Enum contract: which enums are needed and in what order

This topic usually involves **two different enum contracts**, sometimes three:

1. `AppearanceKey enum` (shared)
   These are the keys of the `AppearanceData` dictionary.
   It is these enums that are passed to `SetData` / `TryGetData` / `RemoveData`.

2. `LayerKey enum` (client + prototype visualization)
   These are the keys of the sprite layers (layer map), by which you change `visible/state/color/...`.
   These enums are used in `SpriteSystem` and/or in `GenericVisualizer` YAML as layer key.

3. `AppearanceValue enum` (optional, shared)
   This is not a key, but a **value** of appearance data.
   Example: `ChargeState.Empty/Medium/Full`.

Critically important: `AppearanceKey enum` and `LayerKey enum` are different roles and should not be mixed ⚠

### Order of application in code and YAML

1. Describe `AppearanceKey enum` in the shared contract.
2. Describe `LayerKey enum` for addressing layers.
3. The server writes the value: `SetData(uid, AppearanceKey.SomeKey, value)`.
4. Client:
   - or custom visualizer: reads `AppearanceKey` via `TryGetData`, applies to `LayerKey` via `SpriteSystem`;
   - or GenericVisualizer: YAML mappit `AppearanceKey -> LayerKey -> AppearanceValueString -> PrototypeLayerData`.

### Matching pattern

```yaml
visuals:
  enum.AppearanceKeyEnum.SomeKey:
    enum.LayerKeyEnum.TargetLayer:
      "AppearanceValueToString":
        state: some_state
        visible: true
```

```csharp
[Serializable, NetSerializable]
public enum LockerVisuals : byte // 1) AppearanceKey enum
{
    Open,
    ChargeState,
}

[Serializable, NetSerializable]
public enum LockerVisualLayers : byte // 2) LayerKey enum
{
    DoorOpen,
    DoorClosed,
    Indicator,
}

[Serializable, NetSerializable]
public enum LockerChargeState : byte // 3) AppearanceValue enum (optional)
{
    Empty,
    Medium,
    Full,
}

// Server: AppearanceKey + value only
_appearance.SetData(uid, LockerVisuals.Open, true, appearance);
_appearance.SetData(uid, LockerVisuals.ChargeState, LockerChargeState.Full, appearance);

// Client (custom visualizer): reads AppearanceKey, applies to LayerKey
if (_appearance.TryGetData(uid, LockerVisuals.Open, out bool open, args.Component))
{
    _sprite.LayerSetVisible((uid, args.Sprite), LockerVisualLayers.DoorOpen, open);
    _sprite.LayerSetVisible((uid, args.Sprite), LockerVisualLayers.DoorClosed, !open);
}
```

## API parsing

## 1) `SharedAppearanceSystem`

Key methods:

- `SetData(EntityUid, Enum, object, AppearanceComponent?)`
- `RemoveData(EntityUid, Enum, AppearanceComponent?)`
- `TryGetData<T>(...)`
- `TryGetData(..., out object?)`
- `CopyData(Entity<AppearanceComponent?> src, Entity<AppearanceComponent?> dest)`
- `AppendData(...)`
- `QueueUpdate(...)` (virtual, specific implementation depends on the party)

Important:

- In the current API, appearance data keys are `Enum`.
- Appearance values ​​must be correctly cloned/serializable.

## 2) Client `AppearanceSystem`

What is important to know:

- keeps a queue of appearance updates;
- in `FrameUpdate` causes a change in the visual only for current entities;
- generates and sends `AppearanceChangeEvent`;
- when received, state compares the data and updates the dictionary;
- when cloning appearance data, it requires safe types:
  - value type, or
  - `ICloneable`, or
  - a type that the serializer can copy.

If this is not done, exceptions may occur when applying state ⚠

## 3) Server `AppearanceSystem`

- Issues `AppearanceComponentState` for network synchronization.
- Does not deal with client application of layers and should not know rendering details.

## 4) `VisualizerSystem<T>`

Contract:

- inherited from `VisualizerSystem<TVisualComponent>`;
- redefine `OnAppearanceChange(..., ref AppearanceChangeEvent args)`;
- read the appearance data and apply visual changes on the client.

## 5) `GenericVisualizerComponent`

Basic structure:

- `visuals`: nested format dictionary
  `AppearanceKey -> LayerKey -> AppearanceValueString -> PrototypeLayerData`.

Consequence:

- appearance input values ​​are normalized to a string;
- layer key can be an enum-reference or a regular string.

## 6) `GenericVisualizerSystem`

Application algorithm:

1. Follows the described `visuals`.
2. For each appearance key, it tries to read the current value.
3. Converts the value to a string (`ToString()`).
4. Looks up the corresponding `PrototypeLayerData` in the variant dictionary.
5. Reserves/gets a layer by key.
6. Applies `LayerSetData`.

Practical conclusion:

- `GenericVisualizer` is great for pure declarative mapping;
- for complex branches, animations, interaction with multiple systems, you need a custom visualizer.

## Decision Tree: GenericVisualizer or custom visualizer

1. Do you only need "value X -> state/visible/color/shader/offset/..." without complex logic?
   Use `GenericVisualizer` ✅
2. Do you need timers, animations, external system dependencies or complex calculations?
   Write custom `VisualizerSystem<T>` ✅
3. Do you need to dynamically create/delete many layers at runtime?
   Usually custom visualizer ✅
4. Do you need a composition of several appearance keys with non-trivial rules?
   Usually custom visualizer ✅

## Practical examples

### Example 1: the server writes and clears the appearance flag

```csharp
[NetSerializable, Serializable]
public enum LockerVisuals : byte
{
    Open,
}

private void UpdateLockerAppearance(EntityUid uid, AppearanceComponent appearance, bool open)
{
    // The server reports only the actual visual state.
    _appearance.SetData(uid, LockerVisuals.Open, open, appearance);

    if (!open)
    {
        // If necessary, the key can be explicitly removed.
        _appearance.RemoveData(uid, LockerVisuals.Open, appearance);
    }
}
```

### Example 2: Client visualizer reads typed data

```csharp
protected override void OnAppearanceChange(EntityUid uid, LockerVisualsComponent component, ref AppearanceChangeEvent args)
{
    if (args.Sprite == null)
        return;

    // We read AppearanceKey enum (LockerVisuals), and apply LayerKey enum (LockerVisualLayers).
    if (_appearance.TryGetData(uid, LockerVisuals.Open, out bool open, args.Component))
    {
        _sprite.LayerSetVisible((uid, args.Sprite), LockerVisualLayers.DoorOpen, open);
        _sprite.LayerSetVisible((uid, args.Sprite), LockerVisualLayers.DoorClosed, !open);
    }
}
```

### Example 3: complex payload in appearance

```csharp
[Serializable, NetSerializable]
public sealed class ShowLayerData
{
    public string Key = string.Empty;   // Target layer key.
    public bool Visible;                // Should I show the layer?
    public string? State;               // Optional RSI state.
}

private void PushLayerPayload(EntityUid uid, AppearanceComponent appearance, ShowLayerData data)
{
    // A complex payload is more convenient than a set of disparate Boolean flags.
    _appearance.SetData(uid, MapperVisuals.LayerData, data, appearance);
}
```

### Example 4: Transferring visual data between entities

```csharp
private void CopyAppearance(Entity<AppearanceComponent?> source, Entity<AppearanceComponent?> target)
{
    // Complete replacement of destination data.
    _appearance.CopyData(source, target);

    // Or merge mode (append) for partial enrichment.
    // _appearance.AppendData(source, target);
}
```

### Example 5: GenericVisualizer YAML config (bool -> layer)

```yaml
- type: GenericVisualizer
  visuals:
    enum.LockerVisuals.Open:
      enum.LockerVisualLayers.DoorOpen:
        "True":
          visible: true
      enum.LockerVisualLayers.DoorClosed:
        "True":
          visible: false
        "False":
          visible: true
```

### Example 6: GenericVisualizer YAML config (enum value -> state/shader)

```yaml
- type: GenericVisualizer
  visuals:
    enum.PowerVisuals.ChargeState:
      enum.PowerVisualLayers.Indicator:
        "Empty":
          state: empty
          shader: unshaded
        "Medium":
          state: medium
        "Full":
          state: full
          color: "#99ff99"
```

### Example 7: when you need a custom visualizer instead of a GenericVisualizer

```csharp
protected override void OnAppearanceChange(EntityUid uid, TriggerVisualsComponent component, ref AppearanceChangeEvent args)
{
    if (args.Sprite == null)
        return;

    if (!_appearance.TryGetData(uid, TriggerVisuals.Active, out bool active, args.Component))
        return;

    // This is no longer just mapping: it requires launching animation and additional functions. logics.
    _sprite.LayerSetRsiState((uid, args.Sprite), TriggerLayers.Core, active ? "active" : "idle");
    _animation.Play(uid, "pulse", active);
}
```

## Patterns 🙂

- Keep appearance keys in shared-enum so that the server and client speak the same contract.
- Pass “narrow” and semantic payload structures, rather than an amorphous set of `object`.
- Use `GenericVisualizer` when the task is really declarative.
- For complex logic, use custom `VisualizerSystem<T>` and explicit `OnAppearanceChange`.
- For migrations/transformations of entities, use `CopyData`/`AppendData`.
- Explicitly clean up stale keys via `RemoveData` if they are no longer valid.

## Anti-patterns ❌

- Pass non-clonable/non-serializable reference objects to appearance.
- Use strings instead of enum keys where you can set a shared enum.
- Pass `LayerKey enum` to `SetData/TryGetData` instead of `AppearanceKey enum`.
- Trying to stuff complex branches and animations into `GenericVisualizer`.
- Duplicate the same visual logic in several unrelated visualizer systems.
- Do not delete outdated appearance keys and get a “stuck” visual.
- Mix server state calculation and client layer application in one method.

## Checklist before change ✅

- Is the contract of keys and payload types described in the shared part?
- Are appearance values ​​safe for clone/copy/state sync?
- Have you selected the correct tool: `GenericVisualizer` or custom visualizer?
- Are keys updated/cleared (`SetData`/`RemoveData`) symmetrical?
- Is the client visualizer idempotent and does not depend on the random order of events?
- Visual logic did not go to the server and vice versa?

## Common errors

- In YAML-visuals, the key value does not match the `ToString()` line of the real enum/bool.
- Enum roles are mixed up: `AppearanceKey` and `LayerKey` (the most common mistake during first implementation).
- Custom payload is not serialized over the network and breaks the use of state.
- The logic reads the appearance key, which is never set by the server.
- The renderer updates the wrong layer due to inconsistent layer-keys.
- `GenericVisualizer` is selected for a scenario where animation/timers are really needed.
- Entity migration copied appearance (`CopyData`), but forgot to update dependent client components.
