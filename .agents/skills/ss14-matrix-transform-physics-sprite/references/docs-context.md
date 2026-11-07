# Docs Context (secondary layer)

## Important

- `docs` is used as an auxiliary context, not as a source of truth.
- The main pages for coordinates/transforms/sprites were updated in the `2023-09-21` area.
- Lighting/FOV page updated `2024-02-25`.
- Always confirm current behavior using the current code.

## Squeeze by topic

### Coordinate Systems

- The basic decomposition of spaces has been fixed: `Map`, `Grid`, `Entity`, `Local/World`.
- It is highlighted that errors most often occur at the junction of world/grid and when mixing rotation/translation.
- Practical code correction: in production chains, explicit matrix transitions and `TransformBox` are actively used, and not just coordinate wrappers.

### Transform (Entity Coordinates)

- The idea of relative entity coordinates to the parent is emphasized.
- The importance of the correct parent-link when moving is explained.
- Practical correction to the code: in matrix problems, system world/inv-world matrices and ready-made combined methods are taken so as not to rebuild the chain manually.

### Transform (Grids)

- The role of grid as an intermediate space between map and entity is explained.
- Dependence of tile/query operations on correct grid conversion is shown.
- Practical code correction: for query/culling use `GetWorldMatrix`/`GetInvWorldMatrix` + `TransformBox` for AABB.

### Transform (Physics)

- Docs describe the idea of local reference systems in physics and the need for consistent rotation/position.
- Practical code correction: with broadphase/lookup, `GetRelativePhysicsTransform(...)` is critical, which returns transform already in the desired reference frame.

### Rendering (Sprites and Icons)

- Docks provide a basic model of layers and visual transformations.
- Practical code correction: a modern pipeline is built around `SpriteSystem`, where `LocalMatrix` and layer-strategy matrices are calculated centrally.

### Rendering (Lighting and FOV)

- The general principle of post-process/overlay layers is described.
- Practical code correction: AO/FOV actively do world->target-space transitions via matrix multiplication and then draw/query in local render space or component-tree.

## Summary for skill

- Documentation is useful as theory and terminology.
- For rules and examples, use only fresh fragments of the current code with the `git blame` check.
