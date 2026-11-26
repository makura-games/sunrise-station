---
name: ss14-atmos-system-api
description: Gives a complete practical analysis of the AtmosSystem API in Space Station 14: what methods to use for what, which of them are fresh and safe, which ones are legacy/limited, and how to correctly combine calls in gameplay, devices and map-level logic.
---

# AtmosSystem: API practice

Use this skill when you need to quickly select the right AtmosSystem method and apply it without regressions :)

## Quick method selection

1. You need to get gas around the entity: `GetContainingMixture(...)`.
2. You need gas for a specific tile: `GetTileMixture(...)` or batch `GetTileMixtures(...)`.
3. We need to check air blocking for runtime logic: `IsTileAirBlockedCached(...)`.
4. We need strictly current data immediately after the changes: `IsTileAirBlocked(...)`.
5. It is necessary to light/warm up the atmosphere: `HotspotExpose(...)`.
6. Registration in barotrauma is required: `TryAddDeltaPressureEntity(...)` / `TryRemoveDeltaPressureEntity(...)`.
7. We need a map-level atmosphere: `SetMapAtmosphere(...)`, `SetMapGasMixture(...)`, `SetMapSpace(...)`.
8. We need gas transfer mathematics: `FractionToEqualizePressure(...)` + `MolesToPressureThreshold(...)`.

## Patterns

1. Do atmospheric pre-check via `IsTileSpace + IsTileAirBlockedCached`.
2. For fire/explosions use `HotspotExpose`, do not change hotspot manually.
3. For mass tile requests, use the batch call `GetTileMixtures`.
4. For device-flow, first estimate the target number of moles, then transfer the volume, then `Merge`.
5. For DeltaPressure, maintain lifecycle symmetry: init/add, shutdown/remove, grid-changed remove+add.
6. After geometric changes, always send `InvalidateTile`.
7. Change Map atmosphere through the immutable path (`SetMap*`), and not through manual mutation map-mixture.

## Anti-patterns

1. Rely on `GetAdjacentTileMixtures(..., includeBlocked, excite)` as a fully working API.
2. Use `SetSimulatedGrid` as a working simulation switch.
3. Set `IsTileAirBlocked` in a tight-loop when the cached version is sufficient.
4. Bypass the API and directly pick `GridAtmosphereComponent` collections from gameplay systems.
5. Build new business logic on old LINDA/Superconductivity public methods without isolation.

## Ways to optimize API calls

1. Prefer `IsTileAirBlockedCached(...)` for frequent checks, and leave `IsTileAirBlocked(...)` for rare ones “immediately after invalidation”.
2. Where multiple tiles are checked, use `GetTileMixtures(...)` instead of the `GetTileMixture(...)` series.
3. Don’t set `excite: true` “out of habit”: this adds tiles to active processing and disrupts visuals.
4. After changing the world, use `InvalidateTile(...)` and let `Revalidate` do the hard work.
5. For DeltaPressure, use the lifecycle API (`TryAdd.../TryRemove...`) instead of manual structures and linear searches.
6. For map-level changes, update the state in batches (`SetMapAtmosphere` or `SetMapGasMixture/SetMapSpace`), and not in a series of separate operations.
7. In pipe devices, first count the moles/target pressure, then do one transfer + merge, not a chain of small transfers.

## Code examples

### 1) Pressure regulator: correct transfer calculation

```csharp
// 1) How many moles need to be removed so that inlet does not exceed the threshold.
var deltaMolesToPressureThreshold = AtmosphereSystem.MolesToPressureThreshold(inlet.Air, threshold);

// 2) How many moles are enough not to invert the pressure gradient.
var deltaMolesToEqualize = _atmosphere.FractionToEqualizePressure(inlet.Air, outlet.Air) * inlet.Air.TotalMoles;

// 3) Take the minimum and transfer the corresponding volume.
var deltaMoles = Math.Min(deltaMolesToPressureThreshold, deltaMolesToEqualize);
var removed = inlet.Air.RemoveVolume(volumeToTransfer);
_atmosphere.Merge(outlet.Air, removed);
```

### 2) Lifecycle DeltaPressure API

```csharp
// Init: if the entity is on the grid, add it to processing.
_atmosphereSystem.TryAddDeltaPressureEntity(gridUid, ent);

// GridChanged: remove from old, add to new.
_atmosphereSystem.TryRemoveDeltaPressureEntity(oldGrid, ent);
_atmosphereSystem.TryAddDeltaPressureEntity(newGrid, ent);

// Shutdown: guaranteed cleaning.
_atmosphereSystem.TryRemoveDeltaPressureEntity(currentGrid, ent);
```

### 3) Explosion integration with Atmos API

```csharp
// The explosion does not change the tile manually, but transfers heat through the canonical API.
if (temperature != null)
{
    _atmosphere.HotspotExpose(gridUid, tile, temperature.Value, intensity, causeUid, soh: true);
}
```

### 4) Tile invalidation after changing airtight

```csharp
// After changing the air blockers, mark the tile for the revalidate stage.
_explosionSystem.UpdateAirtightMap(grid, pos, grid);
_atmosphereSystem.InvalidateTile(grid.Owner, pos);
```

### 5) Safe selection of tiles for spawn

```csharp
// We discard space and completely blocked tiles.
if (_atmosphere.IsTileSpace(gridUid, mapUid, tile)
    || _atmosphere.IsTileAirBlockedCached(gridUid, tile))
{
    continue;
}
```

## Rule of thumb

1. By default, use only methods with the status `Fresh-Use`.
2. Use methods with the status `Legacy-Compat` only for compatibility with tests.
3. Do not use methods with the status `Risk/TODO` as support for new rules/API wrappers ⚠️
4. Confirm any API optimization by profiling, and not just by eye.

Keep the API layer thin and predictable: Atmos is “forgiving” little if you bypass the contract 😅
