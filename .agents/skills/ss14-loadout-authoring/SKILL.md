---
name: ss14-loadout-authoring
description: Creation, copying, renaming, migration and review of SS14 loadout prototypes (`roleLoadout`, `loadoutGroup`, `loadout`) in fork folders like `_Scp`: naming, isolation from vanilla, transfer of mandatory equipment from `startingGear`, `startingGear` synchronization and processing of hidden jobs/departments. Use when changing job prototypes and any loadout system files.
---

# SS14 Loadout Authoring

Use this skill as a working standard for the job-loadout system in SS14 :)
Goal: keep role equipment in one predictable graph `job -> roleLoadout -> loadoutGroup -> loadout`, without external references and without being out of sync with `startingGear`.

## Area of responsibility

1. This skill covers `roleLoadout`, `loadoutGroup`, `loadout`, `startingGear` and job prototypes that issue equipment.
2. This skill does not cover the naming of the entire project. For general naming rules, check `ss14-naming-conventions`.
3. This skill does not replace `ss14-upstream-maintenance`: when copying vanilla prototypes to a fork, still keep the changes isolated inside `_ProjectName`.

## Source of truth

1. The source of truth for visible roles is the current loadout prototype graph of the fork.
2. The default source of truth for hidden roles is the existing `startingGear`, unless the user has explicitly asked to migrate them too.
3. The source of truth for visibility in preferences is the current UI logic:
   - job is hidden if `setPreference: false`;
   - department is hidden if `editorHidden: true`;
   - department is also not shown in the character editor if there is no role in it with `setPreference: true`.
4. Do not consider the loadout copy complete until all links from the fork roles are closed to local fork prototypes.

## Mental model

1. `loadout` - one specific variant of an item in a specific slot.
2. `loadoutGroup` - a set of options for one slot or general concept; This is where mandatory is set via `minLimit`.
3. `roleLoadout` - list of groups available to a specific role.
4. `startingGear` - either a legacy layer for hidden roles, or a synchronized default snapshot of the loadout system.
5. For visible roles, do not keep the required equipment in both `startingGear` and the active `roleLoadout` at the same time: this quickly leads to duplicates and out of sync.

## Decision Tree

1. Is the role or department hidden in the preferences?
   - Yes: do not create a new default loadout binding; leave or restore `startingGear`.
   - No: consider the loadout system as the target source of truth.
2. Does the fork role refer to vanilla or someone else's `loadoutGroup`?
   - Yes: copy it locally to the fork and rewrite the link.
3. Does local `loadoutGroup` refer to vanilla or foreign `loadout`?
   - Yes: copy local `loadout` and rewrite the link in the group.
4. Is the required item only in `startingGear`?
   - Yes: transfer it to the loadout system.
5. Does the role already have a group for this slot?
   - Yes: demand to take one item from the group, and not force the old item from `startingGear`.
   - No: create a hidden/required group for the slot and add it to `roleLoadout`.
6. Is `startingGear` needed only as default compatibility?
   - Yes: synchronize it with loadout slots.
   - No: unbind `startingGear` from job after completed migration.

## Naming

### Prototype ID

1. `loadout`: `ScpLoadout` + `ItemPrototypeId`.
2. `loadoutGroup`: `<ForkName(Sunrise, Scp, or another)>LoadoutGroup` + slot name or generic group name.
3. `roleLoadout`: `Job<RoleId>`.
4. If the same `ItemPrototypeId` is needed in two different contexts and without the suffix there will be a collision, add a short context tail: `ScpLoadoutFlashlightLanternCargo`, `ScpLoadoutFlashlightLanternEngineering`.
5. Do not leave new local IDs without a fork prefix.

### Group naming

1. Prefer generic group names that are not tied to a position if the group is actually reusable between roles.
2. Use job-specific group name only if:
   - the role has a unique set of options;
   - the role has another obligation (`minLimit`);
   - the group needs role-specific semantic.
3. Do not name the general group by the vanilla position if the role has already been renamed in the fork.

### File naming and placement

1. File names are `snake_case`.
2. The local path must mirror the original path inside the fork folder.
3. Don't put copies in technical dumps like `Imports/Sunrise` if you can put them in a normal mirror path.
4. If the file refers to a specific role or fork department, use the name of the role and fork department, not the vanilla name.

## Workflow

### 1) Fix the migration area

1. Find all `_Project` job prototypes that the task concerns.
2. Divide them into two groups:
   - visible in preferences;
   - hidden (`setPreference: false`, `editorHidden: true`, department without visible roles).
3. For hidden roles, do not create a new loadout binding without an explicit user request.

### 2) Isolate the graph of loadout links

1. Collect all `roleLoadout` referenced by fork roles.
2. Check each `loadoutGroup` referenced by `roleLoadout`.
3. If the group is not local to the fork, create a copy in the fork and overwrite the role’s link.
4. Check all `loadout` within the group.
5. If `loadout` is not local to the fork, create a copy in the fork and rewrite the link in the group.
6. Repeat until the fork roles have no references to vanilla or other fork prototypes.

### 3) Transfer mandatory equipment from `startingGear`

1. For visible roles, consider the loadout system to be a priority.
2. Complete all `equipment` and the required container items from `startingGear`.
3. If an item is already covered by an existing group for the same slot, don't create a new point loadout just for the sake of the old default.
4. If a slot is already covered by a group, but the role must have it mandatory, make it mandatory through the group (`minLimit` on the group itself or role-specific override of the group).
5. If the slot is not covered by any groups, create a hidden group with a mandatory selection and one local `loadout`.
6. If only one role needs to change the requirement, do not break the shared group for everyone: create a role-specific override.
7. After complete migration, unlink `startingGear:` from job so as not to duplicate the output.

### 4) Maintain synchronization `startingGear`

Use this step when the project is still in hybrid state or the user explicitly asks to synchronize the default spawn.

1. Each significant equipment slot that is in `roleLoadout` must have a compatible default in `startingGear` if the role retains `startingGear`.
2. If `startingGear` already contains an item from the corresponding group, leave it.
3. If the slot is empty, but there is a group, select default like this:
   - first the old item from `HEAD startingGear`, if it is part of the current group;
   - otherwise the most neutral, simple and undecorative item from the group;
   - if there is both a hidden technical group and a regular visible group for one slot, for `startingGear` prefer regular role-playing clothing rather than a technical item;
   - do not fill in personal/cosmetic groups like `trinkets`, `bra`, `pants`, `socks`, unless there is a separate project policy.
4. Synchronize `storage` and `inhand` only when the task clearly requires them.

### 5) Working with hidden roles/departments

1. For jobs with `setPreference: false` by default, leave `startingGear`.
2. For departments with `editorHidden: true` by default, do not create new `roleLoadout`.
3. If department is not hidden, but the UI hides it because it doesn't have any roles with `setPreference: true`, treat it the same as hidden.
4. If someone has already created a loadout binding for hidden roles, and the task does not require this, delete the extra `roleLoadout` and return `startingGear`.
5. Do not create new per-role loadout files for hidden roles just for the sake of mechanically copying the old `startingGear`.

### 6) Check before completion

1. Make sure that all `_Project roleLoadout -> loadoutGroup -> loadout` links are local.
2. Make sure there is no missing `loadoutGroup`.
3. Make sure there is no missing `loadout`.
4. Make sure that new `roleLoadout` are not accidentally created for hidden roles unless they were asked to migrate.
5. If the role leaves `startingGear`, check that the key equipment slots are no longer empty if there are loadout groups on these slots.
6. Run YAML linter.

## Patterns ✅

1. Copy the external `loadoutGroup` to the local fork path and immediately rewrite the role’s link.
2. Copy the outer `loadout` after the group until the graph becomes completely local.
3. Give `loadout` ID in the format `ScpLoadout<ItemPrototypeId>`.
4. Give `loadoutGroup` ID in the format `ScpLoadoutGroup<SlotOrConcept>`, and not according to the historical copy source.
5. Reuse common groups between roles if the slot and semantics really match.
6. Do role-specific override groups if only one role requires a different `minLimit` or another set of options.
7. Transfer required items from `startingGear` to hidden required groups and then remove `startingGear` from the job.
8. Synchronize `startingGear` through existing groups, and not through new one-time copies of items.
9. When choosing a default for an empty slot, choose the most neutral and least decorative option.
10. In case of a dispute between a technical hidden item and regular clothing for one slot, choose the regular clothing of the role for `startingGear`.

## Anti-patterns ❌

1. Leave the `_ForkName` role to refer to the vanilla one or `_Sunrise` `loadoutGroup`.
2. Copy loadout files to `Imports/...` instead of the mirror local path.
3. Give new local IDs vanilla names without a fork prefix.
4. Name files and prototypes by vanilla position, if the role has already been renamed in the fork.
5. Create a new single-item group for a slot that is already covered by a normal shared group.
6. Change the shared group for the sake of one role and accidentally break the remaining roles.
7. Keep the required item simultaneously in the active `roleLoadout` and in `startingGear` if the role has already been completely migrated.
8. Automatically migrate hidden jobs/departments to the loadout system without an explicit request.
9. Synchronize `startingGear` with cosmetics simply because the role has such groups.
10. Consider the migration complete without checking the full link graph and YAML linter.
11. Forgetting to migrate linen from Loadout to `startingGear`

## Examples

### Example 1: Required slot transfer

```yaml
- type: loadout
  id: ScpLoadoutKeyCardMedicalSpecialist
  equipment:
    id: KeyCardMedicalSpecialist

- type: loadoutGroup
  id: ScpLoadoutGroupIdKeyCardMedicalSpecialist
  name: loadout-group-id
  minLimit: 1
  loadouts:
  - ScpLoadoutKeyCardMedicalSpecialist

- type: roleLoadout
  id: JobMedicalSpecialist
  groups:
  - ScpLoadoutGroupIdKeyCardMedicalSpecialist
```

Comment: The required ID card moves from `startingGear` to the hidden required group and becomes part of the loadout graph.

### Example 2: the slot is already covered by the group, do not force the old item

```yaml
- type: loadoutGroup
  id: ScpLoadoutGroupMedicalShoes
  name: loadout-group-shoes
  loadouts:
  - ScpLoadoutClothingShoesColorWhite
  - ScpLoadoutClothingShoesBootsWinterMed

- type: startingGear
  id: MedicalSpecialistGear
  equipment:
    shoes: ClothingShoesColorWhite
```

Comment: if the role already has a shoe group, just set `startingGear` to a compatible default. Don't create a separate hidden group just for old white shoes.

### Example 3: hidden role do not migrate without request

```yaml
- type: job
  id: JudicialInspector
  setPreference: false
  startingGear: JudicialInspectorGear
```

Comment: This default role remains at `startingGear`. A new `roleLoadout` is not needed for it unless the task explicitly requests otherwise.

### Example 4: a shared group should remain shared

```yaml
- type: loadoutGroup
  id: ScpLoadoutGroupSecurityBackpack
  name: loadout-group-backpack
  loadouts:
  - ScpLoadoutClothingBackpackSecurity
  - ScpLoadoutClothingBackpackSatchelSecurity
  - ScpLoadoutClothingBackpackDuffelSecurity
```

Comment: if the set is the same for several security roles, do not duplicate the group for each position. Make a new group only if one role needs a different set or another requirement.

## Checklist before PR

1. All new IDs correspond to the scheme `ScpLoadout*` / `ScpLoadoutGroup*` / `Job<RoleId>`.
2. All new files are located in the mirror local path and are named in `snake_case`.
3. Fork roles do not have references to vanilla or other fork-loadout prototypes.
4. For visible roles, mandatory equipment is not left only in `startingGear`.
5. By default, no extra loadout binding is created for hidden roles.
6. If `startingGear` is left, its equipment slots are synchronized with loadout groups.
7. YAML linter passes.

## Extension rule

1. Add to this skill only what relates specifically to authoring the loadout graph.
2. If there is a large separate topic like UI loadout window, effect-based loadouts or automatic validation scripts, move it to a separate skill or `references/`.
3. When changing local naming rules, update this skill and `ss14-naming-conventions` in a consistent manner.
