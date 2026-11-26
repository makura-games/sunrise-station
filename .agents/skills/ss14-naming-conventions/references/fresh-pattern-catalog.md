# Fresh Pattern Catalog (Naming)

> All entries below have passed the freshness filter: date of modification is not older than 2024-02-20 and without TODO/HACK/FIXME on the topic of naming.

| Domain | Pattern | Confirmed with a recent example | Date | Status |
|---|---|---|---|---|
| Components | `XxxComponent` + `XxxSystem` with common base | `ClickableComponent` + `ClickableSystem` | 2025-05-18 | Use |
| Components | Predicative component name | `ClickableComponent` | 2024-08-09 | Use |
| Systems | System ends with `System` | `EntityPickupAnimationSystem` | 2026-01-08 | Use |
| Dependencies | `IGameTiming` -> `_timing` | `SharedIdCardSystem` | 2025-08-23 | Use |
| Dependencies | `IRobustRandom` -> `_random` | `DrugOverlaySystem` | 2025-06-27 | Use |
| Dependencies | `IPlayerManager` -> `_player` | `DrugOverlaySystem`/`DrunkSystem` | 2025-08-18 | Use |
| Dependencies | `TransformSystem` -> `_transform` | `EntityPickupAnimationSystem` | 2026-01-08 | Use |
| Dependencies | `EntityWhitelistSystem` -> `_whitelist` | `ChangeNameInContainerSystem` | 2024-10-09 | Use |
| YAML components | In the prototype write `- type: Xxx` without `Component` | `- type: Clickable` | 2025-10-12 | Use |
| Prototype IDs | `CamelCase` for ID | `FloorWaterEntity`, `Scp096CryOut` | 2026-01-08 | Use |
| Fork IDs | Fork prefix for fork-only content | `SunriseAmmunition` | 2026-01-14 | Use |
| Prototype fallback | English `name/description` in YAML | `name: emit mournful scream` | 2026-01-08 | Use |
| Ent localization | `ent-MyEntity` + `.desc` | `ent-BasePart`, `.desc` | 2024-08-30 | Use |
| Generic localization | Non-structural keys in `kebab-case` | `armable-examine-armed` | 2025-04-25 | Use |
| Uplink localization | `uplink-*-name` starts with uppercase; `uplink-*-desc` may describe mechanics | `Energy Sword`, `Energy Shield`, `Набор C-4` | 2026-11-02 | Use |
| Variables | Private fields with `_` | `_overlay`, `_transform`, `_whitelist` | 2026-01-08 | Use |
| File naming | C# partial files with a meaningful section suffix | `*.Section.cs` style for partial decomposition | 2025-05-18 | Use |
| File naming | `snake_case` for YAML/FTL | `water.yml`, `armable.ftl` style | 2025-10-12 | Use |

## Brief conclusion

1. The basic associated naming `XxxComponent`/`XxxSystem` is stable and widely repeated.
2. Canonical dependency aliases (`_timing`, `_random`, `_transform`, `_player`, `_whitelist`) have a stable confirmation.
3. Prototypes and localization use a strict split: `CamelCase` ID + `ent-*`/`kebab-case` for FTL.
4. Entity names and uplink card titles have different casing contracts: lowercase for `ent-*`, uppercase for `uplink-*-name`.
