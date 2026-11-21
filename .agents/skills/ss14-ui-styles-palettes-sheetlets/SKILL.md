---
name: ss14-ui-styles-palettes-sheetlets
description: A practical guide to the SS14 style system: StyleClass, palettes, StyleProperties, sheetlets, pseudo-classes and rules composition. Use it when developing and refactoring a visual UI language without hardcode.
---

# Styles, palettes and sheetlets in SS14 UI

This skill only covers the UI style system: classes, palettes, sheetlets and application rules :)
Window layout and network lifecycle UI should be done in separate skills.

## System framework

- `StyleClass`: single point of names of common style classes.
- `StyleProperties`: keys of semantic palette properties.
- `ColorPalette` + `Palettes` + `StatusPalette`: color semantics model.
- `Sheetlet<T>`: style module for a specific group of controls.
- `StylesheetHelpers`: DSL wrappers for convenient assembly of `StyleRule`.

## Pseudo-classes: complete list and when issued

| Pseudo-class | Where is it used | When is exhibited |
|---|---|---|
| `normal` | `ContainerButton`, `TextureButton`, heirs | Normal rendering mode (`DrawModeEnum.Normal`). |
| `hover` | `ContainerButton`, `TextureButton`, `ScrollBar` | The cursor is over the control/grabber, but there is no active click/drag. |
| `pressed` | `ContainerButton`, `TextureButton`, heirs | Control in pressed state (`DrawModeEnum.Pressed`). |
| `disabled` | `ContainerButton`, `TextureButton`, heirs | Control disabled (`DrawModeEnum.Disabled`). |
| `grabbed` | `ScrollBar` | The scrollbar slider is captured by the mouse (drag is active). |
| `placeholder` | `LineEdit`, `TextEdit` | Empty text + placeholder specified, the placeholder is displayed. |
| `notEditable` | `TextEdit` | The control has been converted to read-only (`Editable = false`). |
| `confirm-normal` | `ConfirmButton` | The button is in confirm mode and in its normal state. |
| `confirm-hover` | `ConfirmButton` | Confirm mode + guidance. |
| `confirm-pressed` | `ConfirmButton` | Confirm mode + press. |
| `confirm-disabled` | `ConfirmButton` | Confirm mode + disabled. |

### Code examples: issuing pseudo-classes

```csharp
// Basic Button: Pseudo-class follows DrawMode.
protected override void DrawModeChanged()
{
    switch (DrawMode)
    {
        case DrawModeEnum.Normal:
            SetOnlyStylePseudoClass(StylePseudoClassNormal);
            break;
        case DrawModeEnum.Pressed:
            SetOnlyStylePseudoClass(StylePseudoClassPressed);
            break;
        case DrawModeEnum.Hover:
            SetOnlyStylePseudoClass(StylePseudoClassHover);
            break;
        case DrawModeEnum.Disabled:
            SetOnlyStylePseudoClass(StylePseudoClassDisabled);
            break;
    }
}
```

```csharp
// ScrollBar: grabbed takes precedence over hover.
private void _updatePseudoClass()
{
    if (_grabData != null)
        SetOnlyStylePseudoClass(StylePseudoClassGrabbed);
    else if (_isHovered)
        SetOnlyStylePseudoClass(StylePseudoClassHover);
    else
        SetOnlyStylePseudoClass(null);
}
```

```csharp
// TextEdit: placeholder + notEditable.
private void UpdatePseudoClass()
{
    SetOnlyStylePseudoClass(IsPlaceholderVisible ? StylePseudoClassPlaceholder : null);
    if (!Editable)
        AddStylePseudoClass(StylePseudoClassNotEditable);
}
```

```csharp
// ConfirmButton: Adds the confirm- prefix to the base states.
protected override void DrawModeChanged()
{
    if (IsConfirming)
    {
        switch (DrawMode)
        {
            case DrawModeEnum.Normal:
                SetOnlyStylePseudoClass(ConfirmPrefix + StylePseudoClassNormal);
                break;
            case DrawModeEnum.Pressed:
                SetOnlyStylePseudoClass(ConfirmPrefix + StylePseudoClassPressed);
                break;
            case DrawModeEnum.Hover:
                SetOnlyStylePseudoClass(ConfirmPrefix + StylePseudoClassHover);
                break;
            case DrawModeEnum.Disabled:
                SetOnlyStylePseudoClass(ConfirmPrefix + StylePseudoClassDisabled);
                break;
        }
        return;
    }

    base.DrawModeChanged();
}
```

## All wrapper helpers over `Prop`

Below is a complete list of wrappers from `StylesheetHelpers` that ultimately call `Prop(...)`.

| Helper | What exposes |
|---|---|
| `Modulate(Color)` | `Control.StylePropertyModulateSelf` |
| `Margin(Thickness)` | `Control.Margin` |
| `Margin(float)` | `Control.Margin` (via `Thickness`) |
| `MinWidth(float)` | `Control.MinWidth` |
| `MinHeight(float)` | `Control.MinHeight` |
| `MinSize(Vector2)` | `Control.MinWidth` + `Control.MinHeight` |
| `MaxWidth(float)` | `Control.MaxWidth` |
| `MaxHeight(float)` | `Control.MaxHeight` |
| `MaxSize(Vector2)` | `Control.MaxWidth` + `Control.MaxHeight` |
| `SetWidth(float)` | `Control.SetWidth` |
| `SetHeight(float)` | `Control.SetHeight` |
| `SetSize(Vector2)` | `Control.SetWidth` + `Control.SetHeight` |
| `HorizontalExpand(bool)` | `Control.HorizontalExpand` |
| `VerticalExpand(bool)` | `Control.VerticalExpand` |
| `HorizontalAlignment(HAlignment)` | in the current implementation, helper writes to the key `Control.HorizontalExpand` |
| `VerticalAlignment(VAlignment)` | in the current implementation, helper writes to the key `Control.VerticalExpand` |
| `AlignMode(Label.AlignMode)` | `Label.StylePropertyAlignMode` |
| `Panel(StyleBox)` | `PanelContainer.StylePropertyPanel` |
| `Box(StyleBox)` | `ContainerButton.StylePropertyStyleBox` |
| `Font(Font)` | `Label.StylePropertyFont` |
| `FontColor(Color)` | `Label.StylePropertyFontColor` |

Additionally (not `Prop` wrappers, but often go hand in hand):
- `PseudoNormal()`, `PseudoHovered()`, `PseudoPressed()`, `PseudoDisabled()`
- `MaybeClass(...)`
- `IntoPatch(...)`
- `ParentOf(...)`

Important:
- For real alignment, it is better to set the desired key directly through `Prop(...)`, as long as the helper wrappers `HorizontalAlignment/VerticalAlignment` are not synchronized with the expected behavior.

## Schemes: child by parent and parent by child

### 1) Change child by parent (directly supported)

Scheme:

```text
Parent selector -> Child selector -> Style props child
```

Example from code:

```csharp
// The style of the inner PanelContainer depends on the NanoHeading parent.
return
[
    E<NanoHeading>()
        .ParentOf(E<PanelContainer>())
        .Panel(nanoHeadingBox),
];
```

### 2) Changing parent by child (no direct selector)

This is not done purely with CSS style; the working pattern is as follows:

```text
Child state/event -> code sets parent class/pseudoclass -> style matches parent
```

Recommendation:
- in the parent/container code, set `StyleClass` or a pseudo-class when the state of the child control changes;
- in the stylesheet, describe the rules for the parent for this class/pseudo.

Mini example approach:

```csharp
// Code-behind: the child control has changed state -> mark the parent with a class.
if (childIsWarning)
    parent.AddStyleClass("child-warning");
else
    parent.RemoveStyleClass("child-warning");

// Stylesheet: the parent is styled by the class set from the code.
E<PanelContainer>()
    .Class("child-warning")
    .Modulate(sheet.NegativePalette.Element);
```

## Using textures for UI

### Basic rules

- We take textures through the API stylesheet (`GetTexture`, `TryGetTexture`, `GetTextureOr`), and do not hardcode access to resources directly.
- For stretchable buttons/panels we use `StyleBoxTexture` and patch margins.
- For icons in controls we use `TextureRect.StylePropertyTexture`.

### Code examples

```csharp
// Button texture from stylesheet resources.
var buttonTex = sheet.GetTextureOr(cfg.BaseButtonPath, NanotrasenStylesheet.TextureRoot);

var topButtonBase = new StyleBoxTexture
{
    Texture = buttonTex,
};
topButtonBase.SetPatchMargin(StyleBox.Margin.All, 10);
```

```csharp
// Texture icon style.
E<TextureRect>()
    .Class(OptionButton.StyleClassOptionTriangle)
    .Prop(TextureRect.StylePropertyTexture, invertedTriangleTex);
```

```csharp
// Convenient wrapper for 9-slice.
var styleBox = texture.IntoPatch(StyleBox.Margin.All, 3);
styleBox.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);
```

## Rules for choosing colors for styles

1. First choose a semantic palette:
- `Primary`: interactive elements and main focus.
- `Secondary`: backgrounds and secondary UI.
- `Positive`: confirmation/success.
- `Negative`: danger/error.
- `Highlight`: key accent.

2. For interactive states, follow the chain:
- `Element` -> `HoveredElement` -> `PressedElement` -> `DisabledElement`.

3. For text and background, use the assigned roles:
- text: `Text`/`TextDark`;
- substrates: `Background`/`BackgroundLight`/`BackgroundDark`.

4. Do statuses and scales using `StatusPalette`, and not manual if/hex:

```csharp
var good = Palettes.Status.GetStatusColor(1.0f);
var warning = Palettes.Status.GetStatusColor(0.5f);
var critical = Palettes.Status.GetStatusColor(0.0f);
```

5. `Color.FromHex(...)` is valid in one place: when creating a new palette.

6. For icons/texture buttons, do color variation through `Modulate(...)`, so as not to produce duplicate textures.

## Working with fonts

###What to use

- `sheet.BaseFont.GetFont(size, FontKind)` for specific rules.
- `FontKind`: `Regular`, `Bold`, `Italic`, `BoldItalic`.
- general style classes for typography: `FontSmall`, `FontLarge`, `Italic`, `Monospace`.

### Code examples

```csharp
// Large bold signature for menu buttons.
E<Label>()
    .Class(MenuButton.StyleClassLabelTopButton)
    .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(14, FontKind.Bold));
```

```csharp
// Regular and italic style for status texts.
E()
    .Class(StyleClass.ItemStatus)
    .Prop("font", sheet.BaseFont.GetFont(10));

E()
    .Class(StyleClass.ItemStatusNotHeld)
    .Prop("font", sheet.BaseFont.GetFont(10, FontKind.Italic))
    .Prop("font-color", Color.Gray);
```

### Practice

- Size/typeface should be a function of the role of the text (label, heading, status), and not local taste.
- Same semantic role = same font rule for the project.
- If you need a new typographic pattern, first design it as a general class/rule, then reuse it.

## Patterns 😎

- Build sheetlets small and themed.
- Use `ParentOf(...)` for contextual design of nested controls.
- Store color solutions in palettes, not in elements.
- For buttons and interactivity, first of all think in terms of `normal/hover/pressed/disabled` states.
- Use color modification via `Modulate` if you can get by with just one texture.

## Anti-patterns

- Hardcode colors by place (`Color.FromHex`) instead of palettes.
- Interfere with the visual tasks of UI styles with the tasks of the graphical pipeline of the world.
- Scatter the same font settings across dozens of rules without a common class.
- Try to implement the parent-child style using only a selector, without linking code.

## Quality checklist

- All interactive states are covered by pseudo-classes.
- For each `Prop(...)` operation, helper wrappers are used where possible.
- Contextual styles via `ParentOf(...)` are applied deliberately.
- Textures are connected via the stylesheet API and correct patch margins.
- Colors are taken from semantic palettes and status models.
- Fonts and sizes are set as system rules, not ad-hoc.
