# Rejected Snippets (Naming)

| Zone | What's found | Why rejected | Signal |
|---|---|---|---|
| Dependencies | Areas with variations `_transformSystem`/`_playerManager` in the new code | Breaks canonical short form aliases for strict standard | Alias style inconsistency |
| Systems | System without obvious connection by name base with the main component | Degrades discoverability and API connectivity | Hard to find component/system pair |
| Prototypes | lowercase ID like `clientsideclone` for a production entity | Contradicts strict `CamelCase` for new code | Historical legacy format |
| Prototypes | New IDs without fork prefix in fork-only copies of vanilla | Breaks the namespace and complicates future merge | Potential ID collisions |
| YAML names | Non-English fallback `name/description` | Violates the contract "YAML fallback = english source of truth" | Divergence from English locale |
| FTL keys | Regular keys are not in `kebab-case` | Reduces uniformity and string search | Incompatible key format |
| FTL content | OOC instructions in `.desc` without marker `OOC:` | Violating the IC/OOC Boundary | Content ambiguity |
| FTL content | Entity names longer than 3 words and descriptions over limit | Reduces readability and UI density | Impractical text size |
| Files | `snake_case` 3+ word YAML/FTL names for no good reason | Blurs the structure and makes navigation difficult | Overcomplicated names |
| Examples quality | Fragments from TODO/HACK/FIXME on the topic | Risk of consolidating a controversial or temporary style | Explicit markers of technical debt |
| Freshness | Examples older than cutoff without modern confirmation | Possibly outdated practices | Old modification date |

## How to work with deviations

1. If a fragment is included in this list, do not copy it as a reference into the new code.
2. For legacy places, allow point compatibility, but do not extend the style further.
3. In the review, mark such places as “legacy exception” and return the code to the standard at the next safe refactoring.
