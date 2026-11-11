---
name: ss14-naming-conventions
description: Strict naming standards in Space Station 14 for C#, YAML prototypes and FTL: names of components/systems/dependencies, prototype IDs, localization keys, variables and files. Use it when creating or reviewing new code, prototypes and localization, when you need to check compliance with the naming standard.
---

# SS14 Naming Conventions

This skill sets a single strict naming standard for SS14 code, prototypes and localization :)
Use it as a guideline: if new code/content doesn't follow the rules below, it's a bug, not a "style variation."

## Reading order

1. Read `references/fresh-pattern-catalog.md` first.
2. Then read `references/rejected-snippets.md`.
3. At the end, check the terminology and boundaries of the docs in `references/docs-context.md`.

## Source of truth

1. The source of truth for the rules is the current code base.
2. Documentation is needed for intent, terms and explanations, but does not overwrite the behavior of live code.
3. Examples older than cutoff `2024-02-20` and fragments with TODO/HACK/FIXME on the topic of naming should not be used as a standard.

## Mental model

1. Names in SS14 are a contract between C#, YAML and FTL.
2. Basic purpose of a name: quickly convey the role of an entity/type/key without exposing the implementation.
3. Good naming minimizes renaming when expanding functionality.
4. The new code must be in English in the identifiers and fallback fields of the prototypes.

## Strict rules (MUST/SHOULD)

### 1) Components

1. MUST: the component name ends with `Component`.
2. MUST: name format is `CamelCase`.
3. MUST: the name reflects the behavior in 1-3 words.
4. SHOULD: give priority to forms with an adjective, for example `ClickableComponent`.
5. MUST: if there is a paired system, the base part of the system and component name is the same.
6. MUST: do not write the suffix `Component` in the YAML prototype of `- type:`.

### 2) Systems

1. MUST: the system name ends with `System`.
2. MUST: name format is `CamelCase`.
3. MUST: if there is a target component, use the same base part (`XxxComponent` <-> `XxxSystem`).
4. MUST: if there is no target component, the system name describes the action in 1-3 words.

### 3) Dependency fields

1. MUST: private dependencies start with `_`.
2. MUST: build the dependency name from the base part of the type without `System/Manager`.
3. MUST: `TransformSystem` -> `_transform`, `IPlayerManager` -> `_player`.
4. MUST: adhere to the canonical short forms: `IGameTiming` -> `_timing`, `IRobustRandom` -> `_random`, `EntityWhitelistSystem` -> `_whitelist`.
5. SHOULD: avoid noisy options like `_transformSystem`/`_playerManager` if there is an established short alias.

### 4) Prototype ID

1. MUST: ID format - `CamelCase`.
2. MUST: when inheriting from `BaseXxx`, write the base name in the child ID without the prefix `Base`.
3. MUST: when developing the inheritance chain, increase suffixes on the right (`Meat` -> `MeatCat`).
4. MUST: if the entity is unique to the fork and especially if it is a forked copy of the vanilla entity, add the fork prefix (`ScpXxx`, `SunriseXxx`).
5. MUST NOT: use snake_case, kebab-case or lowercase ID for new production code.

### 5) Name/Description in prototypes and localization

1. MUST: `name` and `description` fallback in YAML be written in English.
2. MUST: fallback content in YAML matches the meaning of the English localization.
3. MUST: the name of the entity in localization is in lower case, maximum 3 words.
4. MUST: description of the entity - with a capital letter, maximum 3 sentences.
5. SHOULD: do not use quotes unless escaping is required.

### 6) Localization keys

1. MUST: regular localization keys - `kebab-case` (`word1-word2-word3`).
2. MUST: for entities use the format `ent-MyEntity`, `.desc`, `.suffix`.
3. MUST: a regular key reflects the meaning of the action/state (`item-pick-up-start`).
4. SHOULD: build new variations by adding suffixes to an existing key, rather than creating an unrelated new tree.

### 7) Localization content

1. MUST: content is written in the language of a specific locale.
2. MUST: write the name/desc of entities in IC style as an external observable description.
3. MUST: Explicitly mark OOC text with the prefix `OOC:`.
4. MUST NOT: Mix in-game descriptions with OOC instructions without a marker.

### 8) Variables in the code

1. MUST: format - `camelCase` for local/parameters, private fields with `_`.
2. MUST: the name reflects the essence of the data.
3. MUST: if the variable stores a component, use the base part of the component name (`ActiveScp096RageComponent` -> `scp096Rage`).
4. MUST NOT: use meaningless names (`data`, `value2`, `tmp`) outside the micro-area.

### 9) File names

1. MUST: `yml/ftl/swsl` — `snake_case`.
2. SHOULD: maximum 2 words if the context is already expressed by folders.
3. MUST: C# file in `CamelCase`, the name matches the key class.
4. SHOULD: if there are several classes/partial parts in the file, the file name describes the subsection in a maximum of 2 words.

## Decision Tree

1. Do you need a new ECS data container?
   Select a basic action/property -> create `XxxComponent`.
2. Do you need logic for the component?
   Use the same database -> `XxxSystem`.
3. Are you adding a component to the prototype?
   Write `- type: Xxx` without `Component`.
4. Selecting a dependency alias?
   Remove `System/Manager` -> reduce to canonical form (`_timing`, `_random`, `_transform`, `_player`, `_whitelist`).
5. Are you creating a fork-only or fork copy of vanilla?
   Add a fork prefix to the ID (`Scp*`, `Sunrise*`).
6. Are you creating a localization key?
   Entity: `ent-MyEntity`; regular string: `kebab-case`.

## Patterns ✅

1. `ClickableComponent` + `ClickableSystem` with a common base part.
2. `- type: Clickable` in YAML instead of `ClickableComponent`.
3. Alias ​​`IGameTiming` as `_timing`.
4. Alias ​​`IRobustRandom` as `_random`.
5. Alias ​​`TransformSystem` as `_transform`.
6. Alias ​​`IPlayerManager` as `_player`.
7. Alias ​​`EntityWhitelistSystem` as `_whitelist`.
8. ID in the style `Scp096CryOut` with a clear domain prefix.
9. Fork ID with the prefix `Sunrise*` for unique fork content.
10. `ent-BasePart = body part` as a short external name of the entity.
11. `armable-examine-armed` is like a regular `kebab-case` non-entity string key.
12. Private component field `scp096Rage` according to the basic part of the type.

## Anti-patterns ❌

1. `Clickable` as the name of a C# component without the `Component` suffix.
2. `ClickableComponentSystem` instead of `ClickableSystem`.
3. `- type: ClickableComponent` in YAML.
4. `TransformSystem` -> `_transformSystem` in the code if there is a canonical `_transform`.
5. `IPlayerManager` -> `_playerManager` in the gameplay system if `_player` is possible.
6. New prototype ID in snake_case or kebab-case.
7. Fork copy of the vanilla entity without the fork prefix.
8. Non-English `name/description` fallback in YAML.
9. Regular localization key in `PascalCase` or with `_`.
10. OOC hint in `.desc` without the `OOC:` marker.
11. Description longer than 3 sentences or name longer than 3 words.
12. Private fields without `_` (except constants).

## Code examples

### Example 1: paired naming of a component and a system

```csharp
[RegisterComponent]
public sealed partial class ClickableComponent : Component
{
}

public sealed class ClickableSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transforms = default!;
}
```

Comment: the base part `Clickable` is the same for the pair `Component/System`; This is a canon related naming.

### Example 2: correct dependency alias for whitelist

```csharp
public sealed partial class ChangeNameInContainerSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
}
```

Comment: The short canonical alias `_whitelist` is used, not `_whitelistSystem`.

### Example 3: correct dependency alias for transform

```csharp
public sealed class EntityPickupAnimationSystem : EntitySystem
{
    [Dependency] private TransformSystem _transform = default!;
}
```

Comment: the base of type `Transform` is moved to `_transform`.

### Example 4: correct dependency alias for random/player

```csharp
public sealed class DrugOverlaySystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
}
```

Comment: `Manager`/`Random` have been normalized to stable short names.

### Example 5: YAML component without Component suffix

```yaml
- type: entity
  id: FloorWaterEntity
  components:
  - type: Clickable
```

Comment: YAML uses the base part of the component name.

### Example 6: fork prefix + CamelCase for prototype ID

```yaml
- type: entity
  id: Scp096CryOut
  name: emit mournful scream
```

Comment: `Scp` prefix and `CamelCase` are respected; action name in English.

### Example 7: Sunrise prefix in fork content

```yaml
- type: guideEntry
  id: SunriseAmmunition
  name: guide-entry-ammunition
```

Comment: The fork identifier is clearly separate from the vanilla namespace.

### Example 8: `ent-*` entity keys and lowercase name

```ftl
ent-BasePart = body part
    .desc = { ent-BaseItem.desc }
```

Comment: entity name is short and lowercase; the description is specified separately via `.desc`.

### Example 9: regular keys in kebab-case

```ftl
armable-examine-armed = {CAPITALIZE(THE($name))} is [color=red]armed[/color].
armable-examine-not-armed = {CAPITALIZE(THE($name))} needs to be armed.
```

Comment: Non-entity keys go to `kebab-case`.

## Checklist before PR

1. All new C# types and fields are named according to the rules above.
2. In YAML there is no `...Component` after `- type:`.
3. Prototype ID in `CamelCase`, fork content has the correct prefix.
4. FTL keys are divided into `ent-*` and `kebab-case` according to their purpose.
5. `name/description` fallback in YAML - in English and consistent with the English locale.
6. There is no OOC text without a `OOC:` marker.

## Extension rule

1. Add a new pattern only if it is confirmed by fresh code and does not conflict with the current MUST standard.
2. First register any exception as an anti-pattern/legacy in `references/rejected-snippets.md`.
3. If a separate large topic appears (for example, only naming UI/XAML), move it to a separate skill and leave only a cross-link here.

Keep naming predictable and uniform: this way reviews are faster and there are fewer regressions 🚀
