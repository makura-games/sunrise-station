---
name: ss14-atmos-system-core
description: Parses the AtmosSystem architecture in Space Station 14 at the server/shared/client level: processing cycle, tile state, invalidations, connection with DeltaPressure and overlay synchronization. Use it when you need to understand how the system actually works, where it is safe to expand, and how not to break the performance/consistency of the atmosphere.
---

# AtmosSystem: Architecture and Cycle

Use this skill as an architectural playbook for AtmosSystem :)
Keep your focus on the current code and check for freshness via `git blame` (cutoff: `2024-02-19`).

## Source of truth

1. The codebase is the primary source of truth.
2. Documentation is a secondary layer for terminology and design intent.
3. Do not add any site older than two years or with TODO on the topic in rules/patterns.

## Mental model of AtmosSystem

1. The system lives around `GridAtmosphereComponent`: each grid has a set of `TileAtmosphere`, queues of the current cycle and a processing-state.
2. Processing occurs in stages through a finite machine: `Revalidate -> TileEqualize -> ActiveTiles -> ExcitedGroups -> HighPressureDelta -> DeltaPressure -> Hotspots -> Superconductivity -> PipeNet -> AtmosDevices`.
3. Each stage must be able to pause according to the time-budget and continue at the next tick.
4. Any change in geometry/airtight first invalidates the tile, and the real revaluation occurs at the `Revalidate` stage.
5. Gas production is based on the chain `grid tile -> map atmosphere -> SpaceGas fallback`.
6. Gas visuals are separated from gas physics: physics changes tile data, and overlay receives invalid chunks and is synchronized by the network.
7. DeltaPressure - a separate subsystem within the cycle: parallel pressure collection + delayed application of damage.

## Patterns

1. Implement the new mechanic as a separate stage or as a separate block in an existing stage with an explicit timer pause.
2. After changing the airtight/tile, always invalidate the tile rather than recalculate everything manually.
3. To read air blocking in gameplay code, prefer the cached version of the check.
4. For map-level atmosphere, use immutable mixture and centralized refresh map-atmos tiles.
5. For entities with barotrauma, use lifecycle registration in DeltaPressure lists (init/shutdown/grid changed).
6. For shared breathing logic, use mask/internal events and maintain correct disconnect.
7. For client gas, combine full/delta state through a single merge pass across chunks.

## Anti-patterns

1. Consider that Atmos updates everything at once in one tick; ignore `ProcessingPaused`.
2. Bypass invalidation and change airtight/tiles caches locally.
3. Use legacy zones with TODO as a reference implementation.
4. Link logic to the order in which devices are called as a “feature”.
5. Write heavy logic without yield points based on time-budget.
6. Assume that docs always match the current behavior of the code.

## Optimization methods when working with the system

1. For frequent gameplay checks, use `IsTileAirBlockedCached(...)`, and not the on-the-fly option.
2. To set tiles, use `GetTileMixtures(...)` instead of multiple single calls.
3. Set `excite: true` only when you really need an immediate response from the atmosphere/visuals.
4. After local changes, call `InvalidateTile(...)`, do not run a manual mass recalculation.
5. In DeltaPressure, select `DeltaPressureParallelProcessPerIteration` and `DeltaPressureParallelBatchSize` for the server load.
6. Don't dirty/refresh the entire grid overlay unnecessarily, invalidate only the affected tiles.
7. In new subsystems, do batch processing: queue + periodic time-budget check.
8. If you need a quick pre-filter of a point, combine `IsTileSpace(...)` + `IsTileAirBlockedCached(...)`.
9. For map-level changes, update the atmosphere in batches (`SetMapAtmosphere/SetMapGasMixture/SetMapSpace`), and not by tile.

## Code examples

### 1) Atmos stages finite state machine

```csharp
// Each stage returns: continue, move to the next, or pause the cycle.
switch (atmosphere.State)
{
    case AtmosphereProcessingState.Revalidate:
        if (!ProcessRevalidate(ent))
            return AtmosphereProcessingCompletionState.Return; // time-budget exceeded

        atmosphere.State = MonstermosEqualization
            ? AtmosphereProcessingState.TileEqualize
            : AtmosphereProcessingState.ActiveTiles;
        return AtmosphereProcessingCompletionState.Continue;

    case AtmosphereProcessingState.DeltaPressure:
        if (!ProcessDeltaPressure(ent))
            return AtmosphereProcessingCompletionState.Return;

        atmosphere.State = AtmosphereProcessingState.Hotspots;
        return AtmosphereProcessingCompletionState.Continue;
}
```

### 2) Invalidation channel: change airtight -> revalidate

```csharp
// When changing the position/state of an airtight entity, mark the tile as invalid.
public void InvalidatePosition(Entity<MapGridComponent?> grid, Vector2i pos)
{
    _explosionSystem.UpdateAirtightMap(grid, pos, grid);
    _atmosphereSystem.InvalidateTile(grid.Owner, pos);
}

// At the Revalidate stage, the tile rebuilds TileData/AirtightData and visuals.
UpdateTileData(ent, mapAtmos, tile);
UpdateAdjacentTiles(ent, tile, activate: true);
UpdateTileAir(ent, tile, volume);
InvalidateVisuals(ent, tile);
```

### 3) DeltaPressure: parallel calculation + delayed damage

```csharp
// The pressure calculation is performed in batches in parallel-job.
var job = new DeltaPressureParallelJob(this, atmosphere, atmosphere.DeltaPressureCursor, DeltaPressureParallelBatchSize);
_parallel.ProcessNow(job, toProcess);

// The damage itself is applied in a separate pass from the results queue.
while (atmosphere.DeltaPressureDamageResults.TryDequeue(out var result))
{
    PerformDamage(result.Ent, result.Pressure, result.DeltaPressure);
}
```

### 4) Shared: integration of breathing mask and internals

```csharp
// When turning off the mask, we try to connect the breathing instrument to the internals of the wearer.
private void OnMaskToggled(Entity<BreathToolComponent> ent, ref ItemMaskToggledEvent args)
{
    if (args.Mask.Comp.IsToggled)
    {
        DisconnectInternals(ent, forced: true);
    }
    else if (_internalsQuery.TryComp(args.Wearer, out var internals))
    {
        _internals.ConnectBreathTool((args.Wearer.Value, internals), ent);
    }
}
```

### 5) Client: correct merge full/delta gas-overlay chunks

```csharp
// The client accepts either full state or delta state, but applies the same via modifiedChunks.
switch (args.Current)
{
    case GasTileOverlayDeltaState delta:
        modifiedChunks = delta.ModifiedChunks;
        // We delete local chunks that are no longer in the server list.
        break;
    case GasTileOverlayState state:
        modifiedChunks = state.Chunks;
        break;
}

foreach (var (index, data) in modifiedChunks)
{
    comp.Chunks[index] = data; // single way of application
}
```

## Extension rule

1. Expand fresh stages and fresh API surfaces first.
2. If you need to change a legacy block, first fix the risk in `references/rejected-snippets.md` and only then make the change.
3. Make any new subsystem configurable and pauseable.
4. Check any “acceleration” with metrics (frame time/atmotics), and not with intuition.

Think of it as a pipeline with queues, not as a "single function atmos" 🧪
