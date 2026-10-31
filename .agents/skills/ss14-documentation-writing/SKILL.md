---
name: ss14-documentation-writing
description: A practical documentation standard in Space Station 14 for C#, SWSL, YAML and FTL: how to write `<summary>`, when to add explanatory comments, how to document partial systems, and how to avoid noisy documentation. Use it when writing, refactoring and reviewing documentation in code, prototypes and localization.
---

# SS14 Documentation Writing

Use this skill as a strict working standard for documentation in SS14 ✍️
Goal: leave only useful documentation that speeds up reviews and reduces the risk of incorrect system expansion.

## Reading order

1. Read `references/fresh-pattern-catalog.md` first.
2. Then read `references/rejected-snippets.md`.
3. At the end, check the context in `references/docs-context.md`.

## Source of truth

1. The source of truth for documentation rules is the current code.
2. Documentation from `docs` is required reading, but is used as a secondary layer (terms, intent, restrictions) because it may lag.
3. For any target subsystem, always look not only at the system itself, but also at its usage in `server/shared/client`, especially unusual calls and non-standard flags.
4. Cut off examples older than cutoff `2024-02-20`.
5. Do not use fragments with `TODO`/`HACK`/`FIXME` as a reference if they affect the documentation itself or show controversial behavior.

## Basic rules

### Language

1. Write documentation in English.
2. Do not distort identifiers and XML tags and do not mix languages ​​in one doc block.

### C# and SWSL

1. Use `/// <summary>` as the main documentation format in your code.
2. Document public methods, public API points and DataField contracts via `summary`.
3. For partial classes, add a short description of the role of the specific part immediately below the class declaration and before the dependency block.
4. For complex mathematics, non-standard state transitions, prediction nuances and workaround logic, add a short comment above the logic block, even if the method already has `summary`.
5. Don't comment out every line and every obvious `if` "for the sake of commenting."
6. Check the validity of XML documents: correct tags and case (`</summary>`, not `</summarY>`).

### YAML prototypes

1. Separate groups of prototypes with comments.
2. If the comment describes the behavior of the prototype, limit it to one sentence.
3. Don’t turn prototypes into “sheets” of multi-line explanations.
4. Do not use edit markers as “documentation” (`Start/End` blocks without meaning for behavior).

### FTL

1. Use comments in FTL only for rare divisions into groups.
2. Always put a space between `##` and the group name: `## Group Name`.
3. Do not use broken headers like `##bombs`.
4. Don't spam headers before every few keys.

## Workflow for documenting the subsystem

1. Find the main contract of the subsystem: what external calls are required to know.
2. View all main callsite in `server/shared/client`.
3. Mark non-standard calls (additional flags, special-case paths, bypassing standard checks).
4. Record these places in `summary` and short point-by-point comments “why is this so”.
5. Remove noise: any comments that simply paraphrase a line of code.

## Decision Tree

1. Is this a public contract/method/type?
   Write `summary`.
2. Is this a complex, non-obvious block (mathematics, prediction, workaround)?
   Write a short comment above the block + leave `summary` at the method level.
3. Is this obvious local code?
   Don't comment.
4. Is this a partial part of a larger system?
   Add the title of the part role and the meaning of the dependency block.
5. Is this a YAML/FTL grouping?
   Give a short separator, without a long description.

## Patterns ✅

1. `ClickableSystem` and `CheckClick(...)` use `summary` for the contract and point comments in places with complex coordinate transformations.
2. `MimePowersSystem.OnInvisibleWall(...)` is documented by `summary`, and comments are left only on critical checks.
3. In `AccessOverriderSystem` public methods combine `summary` and `remarks` when you need to capture a behavior invariant.
4. `BaseContainer` and `SharedContainerSystem.Remove(...)` show a stable API documentation style: `summary` + `remarks` + described parameters.
5. In `SharedScp096System` the logic is decomposed into partial parts with separate blocks of responsibilities and dependencies.
6. `Scp096PhotoSystem` and `ArtifactScp096MadnessSystem` show that the documentation should take into account non-standard call flags (`TryAddTarget(..., true, true)`), and not just the default path.
7. In YAML directories, level separators `# Rank 2` / `# Rank 3` make large lists readable without overload.
8. In FTL, the correct group separator is `## Strings for the battery ...` and does not break parsing.

## Anti-patterns ❌

1. Invalid XML tags in docks (`</summarY>` and other typos).
2. Russian-language and mixed-language doc comments in the code.
3. Obvious comments that simply duplicate a line of code.
4. Using `TODO/HACK/FIXME` as "documentation of behavior" instead of a correction or normal explanation.
5. Multi-line explanations in YAML where one phrase is enough.
6. Edit markers (`Sunrise-Start/End`, `Fire added start/end`) instead of behavioral description.
7. FTL headers without space after `##` (`##bombs`).
8. Frequent or decorative FTL separators (`##########`) that do not help navigation.

## Code examples

### Example 1: `summary` + dot comments on complex logic

```csharp
/// <summary>
/// Handles click detection for sprites.
/// </summary>
public sealed class ClickableSystem : EntitySystem
{
    /// <summary>
    /// Used to check whether a click worked.
    /// </summary>
    public bool CheckClick(...)
    {
        // First we get localPos, the clicked location in the sprite-coordinate frame.
        ...
        // Next check each individual sprite layer using automatically computed click maps.
        ...
    }
}
```

Comment: `summary` holds the API contract, and inline comments remain only for non-obvious steps.

### Example 2: short comments only on really important checks

```csharp
/// <summary>
/// Creates an invisible wall in a free space after some checks.
/// </summary>
private void OnInvisibleWall(Entity<MimePowersComponent> ent, ref InvisibleWallActionEvent args)
{
    // Get the tile in front of the mime
    ...
    // Check if the tile is blocked by a wall or mob, and don't create the wall if so
    ...
    // Make sure we set the invisible wall to despawn properly
    ...
}
```

Comment: Comments do not clutter the method, but only clarify the checks and consequences.

### Example 3: `summary` + `remarks` for invariants

```csharp
/// <summary>
/// Returns true if there is an ID in privileged slot and said ID satisfies access requirements.
/// </summary>
/// <remarks>
/// Other code relies on the fact this returns false if privileged Id is null.
/// </remarks>
private bool PrivilegedIdIsAuthorized(...)
{
    ...
}
```

Comment: `remarks` fixes an invariant that is otherwise easy to break with a refactor.

### Example 4: partial decomposition + analysis of non-standard calls

```csharp
public abstract partial class SharedScp096System
{
    /*
     * Target-handling part of the system.
     */
    [Dependency] private SharedPopupSystem _popup = default!;
    ...
}

if (!_scp096.TryAddTarget((uid, scp096), args.Examiner, true, true))
    continue;
```

Comment: For such systems, document not only “what the part does”, but also why the callsite includes non-standard flags.

### Example 5: YAML grouping without overloading

```yaml
# Rank 2
- type: cargoProduct
  id: CargoDoubleEmergencyTank

# Rank 3
- type: cargoProduct
  id: CargoFulton
```

Comment: A short delimiter speeds up reading and does not turn into an additional "document inside the prototype".

### Example 6: FTL delimiters

```ftl
## Strings for the battery (SMES/substation) menu
battery-menu-out = OUT

##bombs
uplink-pizza-bomb-name = Nefarious Pizza bomb
```

Comment: the first header is correct, the second is broken due to the lack of a space after `##`.

## Checklist before PR

1. All new doc comments in the code are in English.
2. Added `summary` for public and intersystem contracts.
3. In partial systems there is a brief description of the role of the part.
4. Complex blocks received a short explanation "why".
5. In YAML, comments are short and only to the point.
6. In FTL, group comments are rare and only in the format `## Group Name`.
7. There are no invalid XML tags or broken FTL headers.

## Extension rule

1. Add new rules only after confirmation with fresh code and viewing callsite.
2. First fix controversial or legacy examples as anti-patterns.
3. If the topic grows (for example, only guidebook XML or only UI localization), move it to a separate specialized skill.

Document so that the next developer understands the contract without archeology of the code :)
