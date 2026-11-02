---
name: ss14-localization-strings
description: A guide to working with localization files (.ftl) and strings in Space Station 14. Use this skill when adding or changing game text, item descriptions and interface.
---

# SS14 Localization Strings (Russian)

This skill describes the rules and standards for working with localization strings in Space Station 14 (Fluent Translation Lists - FTL).

## Limit of responsibility

This skill covers FTL format, string structure, and localization practices.
Strict naming standards (format `ent-*`, `kebab-case`, name/desc length, English fallback fields in prototypes) are centralized in `ss14-naming-conventions`.
If there is a naming fragment here that diverges from `ss14-naming-conventions`, use `ss14-naming-conventions`.

## 1. File structure and paths

The `.ftl` files are located in `Resources/Locale/{CultureCode}/...`. For Russian this is `Resources/Locale/ru-RU/`.

### File types

* **Entity Prototypes:**
    * They are located in folders corresponding to the prototype structure, often with the prefix `_prototypes`.
    * Example: `Resources/Locale/ru-RU/_prototypes/entities/objects/weapons/guns.ftl`
* **Interface and messages:**
    * They are located in thematic folders (for example, `interaction`, `ui`, `chat`).
    * Example: `Resources/Locale/ru-RU/ui/main-menu.ftl`

## 2. String format and FTL syntax

### Standard Strings (Key-Value)

```ftl
# Simple key
my-system-message-hello = Hello, space!

# Multiline value (indentation required, use SPACE, not TAB)
my-system-popup-error =
    Access denied!
    Please contact an administrator.
```

### Strings for prototypes (Entities)

For entities (`EntityPrototype`), the engine automatically searches for rows by entity ID with the prefix `ent-`.
Use **Fluent** attributes (beginning with a dot) for descriptions and suffixes.

* **Name:** `ent-{PrototypeID} = item name`
* **Description:** Attribute `.desc`
* **Suffix (Editor Suffix):** Attribute `.suffix`

**Example:**
If there is a prototype with `id: Crowbar`

```ftl
ent-Crowbar = crowbar
    .desc = A tool for prying things open.
    .suffix = Tool
```

### 🚫 Anti-pattern: NOT English in YAML prototype
The fields `name`, `description`, `suffix` in the YAML prototype are fallback values.
They must be written in ENGLISH and comply with English localization.

### 🚫 Anti-pattern: Difference between names in YAML and FTL
The fields in the prototype must correspond to the English localization in FTL. Changing one requires changing the other.

### 🚫 Anti-pattern: Lack of tag escaping at the beginning of the line
If a line in an FTL begins with a tag, such as `[bold] Text [/bold]`, the first tag must be escaped.
The parser considers `[` at the beginning of a line to be a broken conditional structure.
If you need to start a line with a tag, you need to escape it using ZERO WHITESPACE.

Example when shielding is required
```ftl
ent-MyItem = item
    .desc =
    [bold] my long description[/bold]
```

Example when shielding is NOT required
```ftl
ent-MyItem = item
    .desc = [bold] my long description[/bold]
```

## 3. Localization inheritance

FTL does not support automatic row inheritance from a parent prototype the way YAML does. If you create a new prototype `CrowbarRed` with `parent: Crowbar`, you **must** create your own localization strings for it, otherwise it will be called `ent-CrowbarRed`.

However, it is possible to reference other lines within the FTL using attribute cross-references:

```ftl
ent-CrowbarRed = red crowbar
    .desc = { ent-Crowbar.desc }
```

## 4. Built-in functions and conditions

SS14 supports special FTL functions for declension and grammar.

### 🧬 Main functions (Functions)

* `THE($ent)`: Adds a definite article (English only).
* `SUBJECT($ent)`: Returns the subject pronoun (he/she/it) based on the gender of the entity.
* `OBJECT($ent)`: Returns the object pronoun (his/her/it).
* `GENDER($ent)`: Returns the gender of the entity (`male`, `female`, `epicene`, `neuter`) for selectors.
* `CAPITALIZE($text)`: Capitalizes the first letter.

### 🔀 Selectors

Used to change text based on gender or number.

**Example (Gender):**
```ftl
examine-verb-details = { GENDER($user) ->
    [male] He examines
    [female] She examines
    *[other] It is considering
} { THE($target) }.
```

**Example (Variables):**
```ftl
# $count - variable passed from code
reagent-container-name = { $count ->
    [one] Test tube
    *[other] Test tubes
}
```

### Shielding
If the text begins with a formatting tag (for example, `[bold]`), escape the opening parenthesis: `{"["}bold]Text`.

## 5. Naming & Style Guide

Follow these rules strictly. They set a unified visual style for the game.

### 📝 Naming Rules

1. **Names of items - with a small letter.**
    * Exception: Proper names or the beginning of a sentence (but in the inventory items are written with a small letter).
    * ✅ `ent-Crowbar = crowbar`
    * ✅ `ent-AccessCard = ID card`
    * ❌ `ent-Crowbar = Crowbar`

2. **Descriptions - capitalized.**
    *These are complete sentences.
    * ✅ `.desc = A useful tool.`
    * ❌ `.desc = a useful tool`

3. **Uplink card names (`uplink-*-name`) are an explicit exception - with a capital letter.**
    * Uplink values are UI titles, so the general lowercase rule for item names does not apply to them.
    * ✅ `uplink-esword-name = Energy Sword`
    * ✅ `uplink-esword-name = Энергетический меч`
    * ❌ `uplink-esword-name = energy sword`

### 🎨 Description Style (Visual Style)

1. **Entity `.desc`: visuals and IC-observable properties only.**
    * The description should talk about what the character *sees* or *feels*.
    * Avoid dry technical data if it is not visible on the item (for example, “Deals 10 damage”).

2. **OOC is only allowed as an explicit OOC block.**
    * OOC phrases must begin with the prefix `OOC:`.
    * Without the `OOC:` marker, adding external game instructions is prohibited.
    * ❌ "Press the G button to activate."
    * ✅ "OOC: Press G button to activate."
    * ✅ "Looks heavy and durable."

3. **Uplink descriptions may explain mechanics.**
    * `uplink-*-desc` is the product card in a game store. It may describe purpose, contents, ammunition, duration, recharge, uses, and other gameplay-relevant properties without an `OOC:` prefix.
    * Do not apply the entity `.desc` “visuals only” restriction to `uplink-*-desc`.
    * Use `OOC:` only when the text steps outside the product description into external instructions such as a keybind or UI operation.

### 🚫 Anti-patterns in FTL

1. **Hardcode paths:** Do not write paths to sprites or sounds in the localization.
2. **Duplicate Keys:** If a key is repeated in different files, the behavior may be unpredictable.
3. **Lack of arguments:** If the line requires the argument `{$user}`, and you did not pass it in the code, there will be an error.
4. **Tab Indentation:** Use spaces for indentation, tabs break the Fluent parser.

## Examples

**Fine:**
```ftl
ent-StandardRadio = handheld radio
    .desc = A portable communication device.

uplink-energy-dome-grenade-name = Shield deployment grenade
uplink-energy-dome-grenade-desc = Deploys a temporary Syndicate energy shield generator on impact.

interaction-popup-blocked = { THE($user) } tries to open the door, but it is locked!
```

**Badly:**
```ftl
ent-StandardRadio = Handheld radio  # Starts with a capital letter
    .desc = used for communication (press T). # OOC info, lowercase

interaction-popup-blocked = The door is closed. # No context for who is trying to open it
```

## 6. Text formatting tags (Rich Text)

List of available formatting tags.

| Tag | Options | Description | Type |
| :--- | :--- | :--- | :--- |
| `color` | `#HEX` / `Name` | Text color. `[color=red]Text[/color]` | Double |
| `font` | `FontID`, `size` | Font/size. `[font=Default size=16]Text[/font]` | Double |
| `bold` | - | **Bold** | Double |
| `italic` | - | *Italics* | Double |
| `bolditalic` | - | ***Bold italics*** | Double |
| `head` | `1`-`3` | Title. `[head=1]Title[/head]` | Double |
| `bullet` | - | List marker ` · ` | Any |
| `cmdlink` | `command` | Executes a command when clicked | Double |
| `textlink` | `link` | Link to process in code (not URL!) | Double |
| `emoji` | `id` | Prototype Emoji | Single |
| `mono` | - | Monospace font (for code) | Double |
| `center` | - | Center alignment | Double |
| `keybind` | `name` | Shows the bind key. `[keybind="MoveUp"]` | Single |
| `scramble` | `rate`, `length`, `chars` | "Encrypted" changing text | Single |
| `protodata` | `text`, `comp`, `member` | Data from prototype (for Guidebook) | Single |
