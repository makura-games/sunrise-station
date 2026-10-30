# Rejected Snippets (Documentation)

| Zone | What's found | Why rejected | Signal |
|---|---|---|---|
| XML docs | Invalid closing tag `</summarY>` | Breaks XML documentation and auto-generation of tooltips | Tag error |
| Language | Multiple languages in a newly authored fork-owned documentation block; a translated or rewritten upstream comment | An upstream block keeps its language, a fork-owned block is read in one language | Merge noise / mixed language |
| Callsite docs | Non-standard API call without explanation (`ignore*` flags) | Behavior is not obvious during review and refactoring | Hidden special-case |
| Code comments | TODO comment as a replacement for behavior documentation | This is technical debt, not a reliable contract | `TODO/HACK/FIXME` |
| YAML comments | Long multi-line "prologues" at the beginning of the prototype file | Prototypes require brevity, maximum 1 sentence of explanation | Wall of text |
| YAML comments | Edit markers (`Start/End`) instead of describing behavior | The system is not explained and quickly becomes outdated | Noise marks |
| FTL headings | Title without space after `##` (`##bombs`) | Breaks format consistency and degrades support | Invalid format |
| FTL headings | Decorative dividers (`##########`) | They do not provide information about the group of transfers | Noise instead of structure |
| Code comments | Step-by-step retelling of obvious operations | Impairs readability and hides really important places | Over-commenting |

## How to use this list

1. Do not copy these fragments as a “style example”.
2. When reviewing, mark such places as technical risks in the quality of documentation.
3. If it cannot be fixed immediately, leave a narrow TODO in the task, but do not turn it into permanent documentation.
