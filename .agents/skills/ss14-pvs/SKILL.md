---
name: SS14 PVS (Potentially Visible Set)
description: Architecture guide for PVS in Space Station 14 — chunk-based spatial partitioning, visibility determination, override types, budgets, Level-of-Detail, leave mechanics, visibility masks, and ExpandPvsEvent
---

# PVS — Potentially Visible Set

## What is PVS

PVS (Potentially Visible Set) is a server system that determines **which entities each client sees**. Instead of sending the entire world to each player, the server filters the data by distance, visibility, and priority.

**Why is this needed:**
- **Traffic savings** - send 500 entities instead of 50,000
- **Protection against cheats** - the client does not receive data that it should not see
- **Performance** - the client processes only visible entities

## Spatial chunking

### Chunk structure

The world is divided into **chunks** of 8x8 units. Each chunk is tied to a **root entity** (map or grid):

```
┌────────────────────────────────────────────────┐
│ Map (MapEntity) │
│  ┌────┬────┬────┬────┐                         │
│ │ 0.0│ 1.0│ 2.0│ 3.0│ ← Card chunks │
│  ├────┼────┼────┼────┤                         │
│  │ 0,1│ 1,1│ 2,1│ 3,1│                         │
│  └────┴────┴────┴────┘                         │
│       ┌────┬────┬────┐                         │
│ │ 0.0│ 1.0│ 2.0│ ← Grid chunks │
│ ├────┼────┼────┤ (separate grid) │
│       │ 0,1│ 1,1│ 2,1│                         │
│       └────┴────┴────┘                         │
└────────────────────────────────────────────────┘
```

### PvsChunkLocation

A chunk is uniquely identified by the `(EntityUid root, Vector2i indices)` pair:
- **root** — `EntityUid` of the map or grid to which the chunk is attached
- **indices** — chunk coordinates in the grid (`position / 8`, rounded down)

### Contents of the chunk

`PvsChunk` stores a **sorted** list of all entities:

1. First - entities with the `MetaDataFlags.PvsPriority` flag
2. Then - anchored entities
3. Then - the remaining direct children
4. Then - children's children (grandchildren)
5. Then - all other descendants recursively

This order is used for **Level-of-Detail** (LoD) - distant chunks can only send part of the content.

### Dirty mechanics of chunks

A chunk is marked as **dirty** when:
- The entity is added to the chunk
- The entity is removed from the chunk
- Entity moves between chunks
- Entity changes parent

At the next PVS update, the dirty chunk rebuilds its `Contents` list.

## Visibility definition

### Viewers

Each session has one or more **viewers** - entities through whose eyes the player sees the world:

- **Main** - `session.AttachedEntity` (player character)
- **Subscriptions** - `session.ViewSubscriptions` (surveillance cameras, ghosts, etc.)

The main viewer is always processed first for prioritization.

### ViewBounds

For each viewer, a visibility rectangle is calculated:

- **Position** — viewer world coordinates + `EyeComponent.Offset`
- **Size** — `NetPvsPriorityRange` × `EyeComponent.PvsScale`

Chunks that intersect this rectangle are considered visible.

### Chunk processing order

Visible chunks are **sorted by distance** to the nearest viewer. It means:
- Nearby chunks are processed first
- If the budget is exhausted, distant entities simply will not enter the state
- This creates the effect of “loading the world from the center”

## Level-of-Detail (LoD)

PVS uses a simple distance based LoD system:

| LoD level | What's being sent | When |
|-------------|-----------------|-------|
| 0 | Only `PvsPriority` entities | Very far |
| 1 | + Anchored entities | Far |
| 2 | + All direct children of the chunk | Average |
| 3 | + Children of children (1 nesting level) | Close |
| 4 | All entities | Inside the regular `NetMaxUpdateRange` |

Entities with `MetaDataFlags.PvsPriority` are visible **at any distance** within the PVS - this is important for walls, doors and other occlusion objects.

## Budget (PVS Budget)

The server limits the number of **new** entities sent to the client per tick:

### Budget options

- **EnterLimit** (CVar: `net.pvs_entity_enter_budget`) — maximum of entities entering PVS for the first time or repeatedly per tick
- **NewLimit** (CVar: `net.pvs_entity_budget`) - maximum of entities that the client has **never seen**

### How it works

```
Every tick for each client:

1. ForceSend entities are processed first → budget is NOT applied
2. The real budget from the CVar is applied
3. Overrides are processed → budget is applied
4. Visible chunks are processed → budget is applied

If the budget is exhausted:
  → Entity is not sent in this tick
  → It will hit the next tick (if still visible)
```

### Definition of an "incoming" entity

An entity is considered “in” PVS if:
- The client sees it for the first time (`EntityLastAcked == 0`)
- She was not in the previous frame (`LastSeen != CurTick - 1`)
- She was not in the last confirmed frame (`EntityLastAcked < FromTick`)
- She left and returned to PVS (`LastLeftView >= FromTick`)

## PVS Override Types

### Hierarchy of overrides

```
┌───────────────────────────────────────────────────────┐
│ ForceSend (global) │
│ • Sent to ALL clients │
│ • Ignores budget │
│ • Ignores visibility mask │
│ • DOES NOT send child │
│ Example: maps, grids │
├───────────────────────────────────────────────────────┤
│ ForceSend (per-session)                               │
│ • Like global, but for one client │
│ Example: (rarely used) │
├───────────────────────────────────────────────────────┤
│ GlobalOverride                                        │
│ • Sent to ALL clients │
│ • Subject to the budget │
│ • Subject to visibility mask │
│ • Sends entity + parents + children │
│ Example: station, singularity, explosions │
├───────────────────────────────────────────────────────┤
│ SessionOverride                                       │
│ • Sent to a specific client │
│ • Subject to the budget │
│ • Subject to visibility mask │
│ • Sends entity + parents + children │
│ Example: minds, SCP-096 for a specific purpose │
└───────────────────────────────────────────────────────┘
```

### API — SharedPvsOverrideSystem

```csharp
// Inject the system
[Dependency] private SharedPvsOverrideSystem _pvsOverride = default!;

// === GlobalOverride ===
// The entity and all the children are visible to everyone. Respects visibility mask and budget.
_pvsOverride.AddGlobalOverride(uid);
_pvsOverride.RemoveGlobalOverride(uid);

// === SessionOverride ===
// The entity and all children are visible to a specific player.
_pvsOverride.AddSessionOverride(uid, session);
_pvsOverride.RemoveSessionOverride(uid, session);

// === SessionOverrides via Filter ===
// The entity is visible to multiple players through a filter.
_pvsOverride.AddSessionOverrides(uid, filter);
```

### API - PvsOverrideSystem (server)

**Additional** methods are available on the server via `PvsOverrideSystem`:

```csharp
[Dependency] private PvsOverrideSystem _pvsOverride = default!;

// === ForceSend (global) ===
// Critical entity - ignores budget and visibility mask.
// DOES NOT send child entities.
_pvsOverride.AddForceSend(uid);
_pvsOverride.RemoveForceSend(uid);

// === ForceSend (per-session) ===
_pvsOverride.AddForceSend(uid, session);
_pvsOverride.RemoveForceSend(uid, session);
```

### When to use what

| Situation | Method |
|----------|-------|
| Global controller (station, glasses) | `AddGlobalOverride` |
| Explosion visible to everyone | `AddGlobalOverride` |
| Player Inventory/Mind | `AddSessionOverride` |
| Surveillance camera | `AddSessionOverride` |
| SCP visible to specific targets | `AddSessionOverride` / `AddSessionOverrides` |
| Map/grid (world critical) | `AddForceSend` (system) |

### Automatic ForceSend

The engine automatically adds to `ForceSend`:
- **All cards** when created (`OnMapCreated`)
- **All grids** when created (`OnGridCreated`)
- **Viewers** sessions - the player entity and cameras are always sent to the owner

## ExpandPvsEvent

For **dynamic** PVS extension, `ExpandPvsEvent` is used. Raised on `AttachedEntity` session every tick:

```csharp
[ByRefEvent]
public struct ExpandPvsEvent
{
    public readonly ICommonSession Session;
    public List<EntityUid>? Entities;           // add these entities
    public List<EntityUid>? RecursiveEntities;  // add these + all children
    public int VisMask;                         // visibility mask for the entire session
}
```

### Usage

```csharp
// Event Subscription
SubscribeLocalEvent<MyComponent, ExpandPvsEvent>(OnExpandPvs);

private void OnExpandPvs(EntityUid uid, MyComponent comp, ref ExpandPvsEvent args)
{
    // Add a remote entity to this client's PVS
    args.Entities ??= new();
    args.Entities.Add(comp.RemoteEntity);

    // Or add an entity with all children
    args.RecursiveEntities ??= new();
    args.RecursiveEntities.Add(comp.Container);
}
```

**Important:** `ExpandPvsEvent` respects visibility masks and PVS budget (unlike `ForceSend`).

## Visibility Masks

### How masks work

Each entity has `MetaDataComponent.VisibilityMask` (default = 1).
Each viewer has `EyeComponent.VisibilityMask` (default = 1).

The entity is sent to the client **only if** the viewer's bitmask **contains** all bits of the entity mask:

```
(eyeMask & entityMask) == entityMask  // true → visible
```

### Examples

```csharp
// The entity is only visible to ghosts
meta.VisibilityMask = (int)VisibilityFlags.Ghost;

// Eye sees both ordinary entities and ghostly ones
eye.VisibilityMask = (int)(VisibilityFlags.Normal | VisibilityFlags.Ghost);
```

### Visibility layers (VisibilityMaskLayer)

Layers are defined in prototypes and used as named flags. Standard layers are defined in `VisibilityFlags`:
- `Normal` (1) - visible to everyone by default
- `Ghost` - visible only to ghosts
- Custom layers for specific mechanics

### Masks and PVS overrides

- **GlobalOverride** and **SessionOverride** - **respect** visibility masks
- **ForceSend** - **ignores** visibility masks
- **ExpandPvsEvent** - you can change the mask through the `VisMask` field

## Entity visibility lifecycle

### Login to PVS

```
1. The server determines that the entity chunk entered the viewer's `ViewBounds`
2. The visibility mask is checked
3. The budget is checked (`EnterLimit`, `NewLimit`)
4. If the budget is not exhausted:
   → The entity is added to ToSend
   → EntityState is formed (full or delta)
   → PvsData.LastSeen = CurTick
```

### Sending a former entity

If the entity was previously visible to the client (EntityLastAcked > 0), upon re-entry the server sends the **delta state** from the last confirmed tick, rather than the full state. This saves traffic.

### Exit PVS

```
1. `ProcessLeavePvs()` checks the `LastSent` list
2. Entities whose LastSeen != current tick → left PVS
3. `MsgStateLeavePvs` is formed (sent `RELIABLY`)
4. `PvsData.LastLeftView = current tick`
5. The client receives `MsgStateLeavePvs`:
   → Sets MetaDataFlags.Detached
   → Moves the entity to null-space
   → The entity is NOT deleted - remains in memory
```

### Detached entities on the client

When an entity leaves the PVS:
- The `MetaDataFlags.Detached` flag is set
- The entity is removed from broadphase (physics) and renderer
- When **re-entering** into PVS:
  - The flag is removed
  - The entity returns to its position
  - The status is updated to the current one

## Server-side data tracking

### PvsData (per-entity, per-session)

For each pair (entity, client), the server stores:

| Field | Description |
|------|----------|
| `LastSeen` | Tick ​​when the entity was last sent to the client |
| `LastLeftView` | Tick ​​when the entity left the client's PVS |
| `EntityLastAcked` | Tick ​​of the last state confirmed by the client that included this entity |

### PvsSession (per-session)

For each session the following is stored:

| Field | Description |
|------|----------|
| `VisMask` | Unified visibility mask for all viewers |
| `Viewers` | List of observer entities |
| `Budget` | Current budget (NewLimit, EnterLimit, counters) |
| `ToSend` | List of entities to send in the current tick |
| `States` | Generated EntityState for GameState |
| `FromTick` | Teak from which deltas are calculated |
| `LastReceivedAck` | Last tick confirmed by the client |
| `RequestedFull` | Client requested full status |
| `Chunks` | Visible chunks sorted by distance |

## PVS processing pipeline (per tick)

```
SendGameStates(players)
│
├─ ProcessDisconnections() - handling disconnections
├─ CacheSessionData(players) - PvsSession initialization
│
├─ BeforeSerializeStates()
│ ├─ ProcessQueuedAcks() - processing confirmations
│ ├─ GetVisibleChunks() - determining visible chunks
│ └─ ProcessVisibleChunks() - updating dirty chunks + cache overrides
│
├─ SerializeStates() - for each player in parallel:
│ ├─ UpdateSession() - update VisMask, Viewers, sorting chunks
│ ├─ AddForcedEntities() - add ForceSend (without budget)
│ ├─ AddAllOverrides() - add GlobalOverride + SessionOverride
│ ├─ ExpandPvsEvent - dynamic expansion of PVS
│ ├─ AddPvsChunks() - add entities from visible chunks
│ └─ ComposeGameState() - compose GameState from States
│
├─ SendStates() - compress and send MsgState
├─ AfterSerializeStates() — clearing dirty buffers, cull deletion history
└─ ProcessLeavePvs() - detect and send MsgStateLeavePvs
```

## CVars for configuring PVS

### Basic

| CVar | Default | Description |
|------|-------------|----------|
| `net.pvs` | `true` | Enable/disable PVS |
| `net.maxupdaterange` | `12.5` | Visibility radius in units |
| `net.pvs_priority_range` | depends | Priority View Radius |
| `net.pvs_entity_budget` | `50` | Max. **new** entities per tick |
| `net.pvs_entity_enter_budget` | `80` | Max. **incoming** entities per tick |

### Debugging

```
// Disable PVS - send everything to everyone (for development)
net.pvs false

// Increase visibility radius
net.maxupdaterange 50

// Increase budget (less pop-in)
net.pvs_entity_budget 200
net.pvs_entity_enter_budget 200
```

### Diagnostic command

```
pvs_override_info <NetEntity>  // Show PVS override information for an entity
```

## Frequent errors

### 1. Forgot to add override for the UI entity

```csharp
// ❌ The essence will disappear if the player is far away
var ui = Spawn("UiEntity", coordinates);

// ✅ Add override so that the UI is always visible
var ui = Spawn("UiEntity", coordinates);
_pvsOverride.AddSessionOverride(ui, session);
```

### 2. Used ForceSend instead of GlobalOverride

```csharp
// ❌ ForceSend ignores budget and visibility - overload when used en masse
_pvsOverride.AddForceSend(uid);

// ✅ GlobalOverride respects budgets - safer for most cases
_pvsOverride.AddGlobalOverride(uid);
```

### 3. Override was not cleared when deleting

```csharp
// ✅ The engine automatically clears overrides when deleting an entity
// But if you manually manage temporary overrides, delete them yourself
_pvsOverride.RemoveSessionOverride(uid, session);
```

### 4. The visibility of child entities was not taken into account

```csharp
// GlobalOverride and SessionOverride add children recursively.
// ForceSend DOES NOT add children!

// ❌ If the container is in ForceSend, the items inside may not be visible
_pvsOverride.AddForceSend(containerUid);

// ✅ For containers use GlobalOverride or SessionOverride
_pvsOverride.AddGlobalOverride(containerUid);
```

### 5. Entity jumps during re-entry

When the entity returns to PVS, the client may see the position "jump". This is normal behavior - the entity is updated to the current state instantly.

You can reduce the effect:
- By increasing `net.maxupdaterange` (more radius → less jumps)
- By increasing `net.pvs_entity_enter_budget` (bigger budget → faster loading)

## Connection with other skills

- **SS14 Netcode Architecture** - how PVS integrates into the overall network stack
- **SS14 Prediction** - how prediction works with PVS detach/enter
- **SS14 ECS Components** — `[NetworkedComponent]`, `Dirty()`, `MetaDataFlags`
- **SS14 ECS Entities** — `EntityUid` vs `NetEntity`, entity life cycle
- **SS14 ECS Systems** - integration of PVS overrides through systems
