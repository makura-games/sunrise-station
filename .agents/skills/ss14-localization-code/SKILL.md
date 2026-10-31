---
name: ss14-localization-code
description: A guide to using localization in Space Station 14 C# code. Describes the ILocalizationManager, LocId, and proper dependency injection patterns.
---

# SS14 Localization in Code (Russian)

This skill describes the rules for working with localization (`ILocalizationManager`) in the C# code of Space Station 14.

## 1. Getting localization: String vs LocId

### LocId (Localization Identifier)
In modern SS14 code, the `LocId` structure should be used to store locale keys instead of a pure `string`. This allows static analyzers to check the existence of keys.

```csharp
// ✅ Good: Using LocId in components and events
[DataDefinition]
public partial struct ExaminedEvent
{
    public LocId Message;
}

// ❌ Bad: Using string for localization keys
public string Message;
```

### Formatting strings
To substitute variables in the FTL message, `(string key, object value)` tuples are used.

```csharp
// FTL:
// my-message = Hello, { $name }! You have { $count } coins.

// C#:
var msg = _loc.GetString("my-message", ("name", "Urist"), ("count", 10));
```

## 2. Using LocalizationManager

The only correct way to work with localization in systems (`EntitySystem`) and controllers is through Dependency Injection.

### ✅ Pattern: Dependency Injection

```csharp
using Robust.Shared.Localization;

public sealed class MyNotSystem : SomeBaseClass
{
    // We implement the manager through the [Dependency] attribute
    [Dependency] private ILocalizationManager _loc = default!;

    public void DoSomething()
    {
        // Using an embedded instance
        var text = _loc.GetString("my-localization-key");
    }
}
```

### 🚫 Anti-pattern: Manual Dependency Injection into EntitySystem
`EntitySystem` already have a `ILocalizationManager` named `Loc`. Additionally, creating it yourself is NOT REQUIRED

### 🚫 Anti-pattern: Manual Resolve
Never use `IoCManager.Resolve<T>()` inside systems or methods where `[Dependency]` can be used. This violates the principle of inversion of control and complicates testing.

```csharp
// ❌ VERY BAD
public void BadMethod()
{
    var loc = IoCManager.Resolve<ILocalizationManager>(); // NO!
    loc.GetString("...");
}
```

### 🚫 Anti-pattern: Static class Loc
The `Loc` class is a static wrapper around `ILocalizationManager`. Its use in `EntitySystem` is considered **deprecated** and deprecated because it is a hidden dependency.

```csharp
// ❌ Poor (within systems)
var text = Loc.GetString("my-key");

// ✅ Okay
var text = _loc.GetString("my-key");
```

**Exception:** Static `Loc` is only valid in places where dependency injection is not possible (e.g. static utility methods, extension methods without IoC access), but even there it is better to pass `ILocalizationManager` as a method argument.

### 🚫 Anti-pattern: String concatenation
Never concatenate localized strings with variables via `+` or `$` (interpolation).
Word order differs in different languages ​​(SVO vs SOV). Fluent supports safe argument substitution.

```csharp
// ❌ BAD: Breaks the grammar of other languages
var text = "Player " + _loc.GetString("traitor-title") + " won!";

// ✅ GOOD: Passing arguments to FTL
// traitor-win-msg = Player { $role } wins!
var text = _loc.GetString("traitor-win-msg", ("role", roleName));
```

## 3. Automatic entity localization

You don't need to manually get the entity name via `_loc.GetString("ent-...")`.
The `Name` and `Description` properties in `EntityPrototype` and the `MetaData` component already do this for you.

```csharp
// Getting the localized name of an entity
var name = Identity.Name(uid, EntityManager); // Takes into account ID cards, masking, etc.
// OR (raw prototype name)
var protoName = prototype.Name; // Already localized
```

## 4. Grammatical attributes (Gender)

When passing entities to localization messages, the engine automatically tries to determine the gender and name. For this to work correctly, pass the entity itself (`EntityUid`), and not just its name as a string.

```csharp
// FTL:
// emote-jump = { THE($entity) } jumps!

// C#:
// ✅ Good: Pass the EntityUid, the engine will find the gender and name
_loc.GetString("emote-jump", ("entity", uid));

// ❌ Bad: We just pass the name, the THE() and GENDER() functions will not work
_loc.GetString("emote-jump", ("entity", Name(uid)));
```

## Code examples

### Registering a component with LocId

```csharp
[RegisterComponent]
public sealed partial class VendingMachineComponent : Component
{
    [DataField]
    public LocId DenyMessage = "vending-machine-deny"; // Default value
}
```

### Use in the system

```csharp
public sealed class VendingMachineSystem : EntitySystem
{
    // We do not import ILocalizationManager, since it is built into EntitySystem as Loc
    [Dependency] private SharedPopupSystem _popup = default!;

    public void OnDeny(Entity<VendingMachineComponent> ent)
    {
        // We get a string from the component and show the popup
        var msg = Loc.GetString(ent.Comp.DenyMessage);
        _popup.PopupEntity(msg, ent, PopupType.Small);
    }
}
```
