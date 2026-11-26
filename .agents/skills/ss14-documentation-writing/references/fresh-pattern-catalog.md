# Fresh Pattern Catalog (Documentation)

> All entries below have passed the freshness filter: the modification date is not older than 2024-02-20 and without TODO/HACK/FIXME on the documentation topic.

| Domain | Pattern | Confirmed with a recent example | Date | Status |
|---|---|---|---|---|
| Engine API docs | `summary` + `remarks` + parameterized contract description | `SharedContainerSystem.Remove(...)` | 2026-01-18 | Use |
| Engine architecture docs | Documentation of the abstract container API through properties and methods | `BaseContainer` | 2024-05-26 | Use |
| Shared gameplay docs | `summary` on actions + short comments on critical checks | `MimePowersSystem.OnInvisibleWall(...)` | 2025-09-05 | Use |
| Client gameplay docs | `summary` + explanation of complex transformations, not all lines in a row | `ClickableSystem.CheckClick(...)` | 2025-05-18 | Use |
| Server gameplay docs | `summary` + `remarks` for an invariant important for calls to other methods | `AccessOverriderSystem.PrivilegedIdIsAuthorized(...)` | 2025-12-16 | Use |
| Large subsystem docs | Dividing a large system into partial parts with a separate area of responsibility | `SharedScp096System` family | 2026-01-24 | Use |
| Cross-system docs | Check for unusual API calls in system consumers | `Scp096PhotoSystem`, `ArtifactScp096MadnessSystem` + `TryAddTarget(..., true, true)` | 2026-01-08 / 2025-11-29 | Use |
| YAML docs | Concise group comments (1 line) between prototype blocks | `salvage reward` catalog groups (`# Rank 2`, `# Rank 3`) | 2025-09-18 | Use |
| FTL docs | Correct group separator in the format `## Group Name` | `battery menu` localization section | 2025-04-28 | Use |
| FTL docs hygiene | Checking the validity of headers before merging | control on the line `##bombs` in the uplink directory | 2026-02-01 | Check |

## Short output

1. `summary` remains the main contract documentation format in C#.
2. Pointed comments are needed where the logic is not obvious (coordinates, invariants, non-standard branches).
3. YAML/FTL benefits from conciseness: short separators and a minimum of “explanatory walls”.
