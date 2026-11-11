---
name: SS14 ECS Prototypes
description: YAML prototypes in Space Station 14 — entity definitions, field inheritance, prototype classes, YAML linter, naming conventions, and localization
---

# Prototypes - prototypes in ECS

## Limit of responsibility

This skill covers prototype mechanics: YAML structure, inheritance, `DataField` mapping, prototype types, and validation.
Strict naming standards (prototype ID, fork prefixes, English fallback fields, `ent-*` and `kebab-case` keys) are maintained in `ss14-naming-conventions`.
If the naming example differs from `ss14-naming-conventions`, use `ss14-naming-conventions`.

## What is Prototype

A prototype is a YAML data definition that the engine loads upon initialization. Prototypes describe entities, recipes, reagents, and other game objects. Entity prototypes determine what components and with what values ​​the entity will have when created.

## ID format - quick reminder (see ss14-naming-conventions)

All prototype identifiers are **mandatory** recorded in CamelCase:

```yaml
# Right
- type: entity
  id: Scp096

- type: entity
  id: BaseScp

- type: entity
  id: MobCombat

- type: entity
  id: Scp096CryOut

- type: entity
  id: XenoArchTriggerHeat

- type: entity
  id: WeaponArcClaw
```

```yml
# Wrong
- type: entity
  id: scp_096          # underscores

- type: entity
  id: scp-096          # hyphens

- type: entity
  id: SCP096           # all uppercase

- type: entity
  id: scp096           # all lowercase
```

## Entity prototype - basic structure

```yaml
- type: entity
  id: MyEntity
  parent: BaseParent        # Inherits from a single parent
  suffix: Debug             # Spawn menu marker (optional)
  components:
  - type: Sprite
    sprite: path/to/sprite.rsi
    layers:
    - state: idle
  - type: MyAction # Original name is MyActionComponent. The `Component` suffix is omitted in the prototype!
    speed: 5.0
    activationSound:
      path: /Audio/sound.ogg
```

## Prototype inheritance

### Single inheritance

```yaml
- type: entity
  id: BaseAnimal
  abstract: true           # Abstract — cannot be spawned directly
  components:
  - type: MobState
  - type: Damageable

- type: entity
  id: Cat
  parent: BaseAnimal       # Inherits all components from BaseAnimal
  components:
  - type: Sprite
    sprite: animals/cat.rsi
```

### Multiple inheritance

```yaml
- type: entity
  id: Scp096
  parent:
  - BaseScp                # First parent has the highest priority on conflicts
  - MobCombat
  - MobBloodstream
  - StripableInventoryBase
  # Local fields and components have priority over every parent.
  components:
  - type: Scp096
    # ...specific fields
```

The order of the parents matters. Parent compositions are applied in the listed order, while already
composed values are treated as child values for the next parent. Therefore, when multiple parents define
the same component field, the first parent wins. Fields and components declared directly on the child
prototype have priority over all parents.

## Field inheritance system

By default, a child prototype field **completely overwrites** the parent value. This behavior is modified by the attributes:

### `[AlwaysPushInheritance]` — merging

Instead of overwriting, it **merges** the parent and child data. Merging works recursively at the level of YAML mappings and sequences.

```yaml
# Parent
- type: entity
  id: BaseEntity
  abstract: true
  components:              # `components` is marked with [AlwaysPushInheritance]
  - type: Sprite
    sprite: base.rsi

# Descendant
- type: entity
  id: ChildEntity
  parent: BaseEntity
  components:
  - type: MyComponent      # ADDED to the parent components
    value: 5
```

Result: `ChildEntity` has both `Sprite` (from its parent) and `MyComponent` (its own).

Main Applications:
- **`components`** in `EntityPrototype` - child components **merge** with parent ones
- **Action lists** (`ActionGrantComponent.actions`) - child actions are **added** to the parent ones
- **Recipe lists** (`LatheRecipePackPrototype`) - child recipes are added

### `[NeverPushInheritance]` - inheritance blocking

The field value is **never** passed from parent to child. The child receives the default value.

```csharp
[NeverPushInheritance]
public bool Abstract { get; private set; }  // abstract is not inherited

[NeverPushInheritance]
public HashSet<ProtoId<EntityCategoryPrototype>>? Categories;  // categories are not inherited
```

Used for:
- `abstract` - the child is not abstract, even if the parent is abstract
- `categories` — categories are individual for each prototype
- Unique identifiers - IDs, visual data that should not cascade

### `[AbstractDataField]` - abstract prototype

Marks the `abstract` field in the prototype. Abstract prototypes:
- Not indexed via `IPrototypeManager`
- Not displayed when listing prototypes
- Serve only as a source of data for inheritance

```yaml
- type: entity
  id: BaseMob
  abstract: true       # This prototype is only a template
  components:
  - type: MobState
  - type: Damageable
```

### `[ParentDataField]` - parent field

Marks a field containing a reference to parent prototypes. In `EntityPrototype` this field is `Parents`.

## Mapping C# → YAML

Component fields with `[DataField]` are serialized in YAML via camelCase:

```csharp
// C# component
[DataField]
public float BaseSpeed = 5f;

[DataField]
public TimeSpan RageDuration = TimeSpan.FromMinutes(4);

[DataField]
public SoundSpecifier RageSound = new SoundPathSpecifier("/Audio/scream.ogg");

[DataField]
public DamageSpecifier CryOutDamage = new();

[DataField]
public EntityWhitelist? CryOutWhitelist;
```

```yaml
# YAML prototype
- type: entity
  id: MyCreature
  components:
  - type: MyComponent
    baseSpeed: 5.0
    rageDuration: 240         # seconds (TimeSpan)
    rageSound:
      path: /Audio/scream.ogg
      params:
        volume: 20
        maxDistance: 30
    cryOutDamage:
      types:
        Structural: 850
    cryOutWhitelist:
      tags:
      - Wall
      - Window
```

### Custom field name

```csharp
[DataField("customYamlName")]
public float SomeCSharpName;
```

```yaml
  customYamlName: 5.0    # Custom name is used
```

## Creating your own prototype type

### Interfaces

- `IPrototype` — basic, required `string ID`
- `IInheritingPrototype` — adds support for inheritance (`Parents`, `Abstract`)

### Simple prototype (no inheritance)

```csharp
[Prototype]
public sealed partial class MyTriggerPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId Tip;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public ComponentRegistry Components = new();
}
```

```yaml
- type: MyTrigger
  id: HeatTrigger
  tip: trigger-heat-tip
  components:
  - type: Temperature
    minValue: 500
```

### Prototype with inheritance

```csharp
[Prototype]
public sealed partial class MyInheritingPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<MyInheritingPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance, AbstractDataField]
    public bool Abstract { get; private set; }

    [DataField]
    public float Value;
}
```

### Attribute `[Prototype]`

```csharp
[Prototype]                        // The type name is derived from the class name
[Prototype("customTypeName")]      // Custom type name for YAML
[Prototype(loadPriority: -1)]      // Download priority
```

The type `type` in YAML is calculated from the class name: `MyTriggerPrototype` → `MyTrigger` (the `Prototype` suffix is ​​discarded).

## Links to prototypes in code

```csharp
// Typed entity prototype reference
[DataField]
public EntProtoId SpawnEntity = "DefaultEntity";

// Typed reference to an arbitrary prototype
[DataField]
public ProtoId<DamageModifierSetPrototype> DamageModifier = "Default";

// Getting a prototype in the system
[Dependency] private IPrototypeManager _proto = default!;

var proto = _proto.Index<MyPrototype>("protoId");
if (_proto.TryIndex<MyPrototype>("protoId", out var proto))
{
    // proto available
}
```

## Naming and localization (name / description)

### Localization system via FTL

Names and descriptions are specified **not** in YAML, but through localization files (`.ftl`):

```text
Resources/Locale/en-US/_prototypes/.../myentity.ftl
Resources/Locale/ru-RU/_prototypes/.../myentity.ftl
```

FTL format:
```ftl
ent-MyEntityId = entity name
    .desc = Entity description goes here.
    .suffix = Debug variant
```

### Naming rules

1. **Names with lowercase letters**: `ent-Scp096CryOut = emit mournful scream`
2. **Descriptions with a capital letter**: `.desc = A strange creature that reacts to being seen.`
3. **In the YAML prototype - only fallback in English**: the `name` and `description` fields in YAML serve as a fallback if there is no localization
4. **Names are inherited from the parent** automatically via FTL - the child uses the parent’s name if its own is not specified
5. **`suffix`** - for marking options in the spawn panel, not visible to players

### Internal Entities

```yaml
- type: entity
  id: InternalEntity
  save: false                    # Not saved on the map
  categories: [ HideSpawnMenu ] # Not shown in the spawn panel
  components:
  - type: MyComponent
```

`save: false` - the entity will not go to the map file when saving.
`categories: [HideSpawnMenu]` - hides the entity from the spawn panel.

## Content.YAMLLinter

A separate project `Content.YAMLLinter` is a tool for validating all YAML prototypes.

### What it checks

1. **Correctness of fields** - all `[DataField]` in YAML must exist in the C# component
2. **Prototype links** - `ProtoId<T>` and `EntProtoId` refer to existing prototypes
3. **Client-server validation** - checks YAML on both sides, takes into account client-only and server-only types
4. **Inheritance validation** - correctness of the `parent` chain

### How it works

- Runs server and client through integration tests
- Calls `IPrototypeManager.ValidateDirectory` for all prototype directories
- Calls `ValidateStaticFields` to check references in code
- Combines mistakes from both sides

### In CI

YAMLLinter runs automatically in CI/CD. Errors block the merge. Error output format:
```text
::error in Prototypes/file.yml(42,5)  Unknown field 'nonExistentField' for component 'MyComponent'
```

## Special YAML constructs

### Typed nodes

```yaml
containers:
  my_slot: !type:ContainerSlot     # Specific container type
  storage: !type:Container          # Regular container
```

### Nested mappings

```yaml
- type: entity
  id: MyEntity
  components:
  - type: Bloodstream
    bloodReferenceSolution:
      reagents:
      - ReagentId: Water
        Quantity: 300
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeAabb
          bounds: "-0.25,-0.4,0.25,0.4"
        density: 500
        mask:
        - MobMask
        layer:
        - MobLayer
```

### Whitelist / Blacklist

```yaml
- type: MyComponent
  pickupBlacklist:
    components:
    - Gun
    - MeleeWeapon
    - Storage
  targetWhitelist:
    tags:
    - Wall
    - Window
```

### Damage (DamageSpecifier)

```yaml
  damage:
    types:
      Slash: 27
      Structural: 110
      Bloodloss: 5
```

### Sounds

```yaml
  # File path
  sound:
    path: /Audio/sound.ogg
    params:
      volume: -4
      maxDistance: 10
```

```yml
  # Sound collection
  sound:
    collection: GibCollection
    params:
      variation: 0.125
```
