---
name: SS14 UI XAML
description: A practical guide to SS14 XAML interfaces: window structure, GenerateTypedNameReferences, loading via RobustXamlLoader, layout containers, localization and style classes. Use it when creating, refactoring and visually polishing UI windows.
---

# XAML and UI windows in SS14

This skill only covers XAML and code-behind for windows/controls :)
The network part (`EUI`, `UserInterfaceSystem`, `UserInterfaceManager`) and deep styling (palettes/sheetlets) should be done in separate skills.

## Rigid XAML binding and `xaml.cs`

This is a mandatory rule, not a recommendation ⚠️

1. The class name in `xaml.cs` and the file name `.xaml` must match one-to-one.
2. The class must be `partial` if `[GenerateTypedNameReferences]` is used.
3. The root type in XAML must be the same as the class in `xaml.cs` or its base class.
4. There must be exactly one matching `.xaml` for the class.

If these requirements are violated, the typed-name references generator throws compile-time errors (`RXN0001`, `RXN0002`, `RXN0005`).

Mini-example of a correct link:

```text
AdminPanel.xaml
AdminPanel.xaml.cs -> public sealed partial class AdminPanel : FancyWindow
```

## Statically retrieving data in XAML

For static values ​​use `x:Static` and `x:Type`.

When needed:
- size/parameter constants from C#;
- enum/static fields for hotkeys and configurations;
- static style classes from `StyleClass`;
- passing `Type` to properties expecting a window/control type.

Examples:

```xml
<!-- Static enum field -->
<ui:MenuButton BoundKey="{x:Static is:ContentKeyFunctions.OpenGuidebook}" />

<!-- Static height constant -->
<ContainerButton MinHeight="{x:Static ui:ContextMenuElement.ElementHeight}" />

<!-- Static string style class constant -->
<ui:MenuButton AppendStyleClass="{x:Static style:StyleClass.ButtonSquare}" />

<!-- Passing `Type` instead of a string -->
<cc:UICommandButton WindowType="{x:Type at:AddAtmosWindow}" />
```

Pattern:
- If the value is “architectural” and is already declared in the code as `const/static`, pull it into XAML via `x:Static`, rather than duplicating it with a literal.

Anti-pattern:
- Duplicate key/class/type lines in XAML manually when there is a static source field.

## UiDependency in the element code

The current codebase does not have a separate `UiDependency` attribute.
The practical equivalent for UI elements is:

1. Use `[Dependency]` for normal IoC element dependencies.
2. Call `IoCManager.InjectDependencies(this)` in the constructor after `RobustXamlLoader.Load(this)`.
3. For `UIController` and dependencies on `EntitySystem` use `[UISystemDependency]`.

Example for a UI element:

```csharp
[GenerateTypedNameReferences]
public partial class FancyWindow : BaseWindow
{
    [Dependency] private IEntitySystemManager _sysMan = default!;
    [Dependency] private IStylesheetManager _styleMan = default!;

    public FancyWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
    }
}
```

Example for `UIController`:

```csharp
public sealed class GuidebookUIController : UIController
{
    [UISystemDependency] private readonly GuidebookSystem _guidebookSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
}
```

## Table of useful UI elements

| Element | When to use | Mini-example |
|---|---|---|
| `FancyWindow` | Standard game windows with title/close | ``<ui:FancyWindow Title="{Loc 'ui-title'}">...</ui:FancyWindow>`` |
| `Control` | Basic wrapper container without unnecessary behavior | ``<Control MinWidth="200">...</Control>`` |
| `BoxContainer` | Vertical/horizontal flow of elements | ``<BoxContainer Orientation="Vertical" SeparationOverride="4">...</BoxContainer>`` |
| `GridContainer` | Table layout with columns | ``<GridContainer Columns="3">...</GridContainer>`` |
| `ScrollContainer` | Scrolling long content | ``<ScrollContainer VerticalExpand="True">...</ScrollContainer>`` |
| `PanelContainer` | Background/frame/visual block separation | ``<PanelContainer StyleClasses="BackgroundPanel" />`` |
| `Label` | Plain text, headings, short captions | ``<Label StyleClasses="LabelSubText" Text="{Loc 'ui-label'}" />`` |
| `RichTextLabel` | Markup/multiline rich text | ``<RichTextLabel Name="Description" />`` |
| `LineEdit` | String input and search | ``<LineEdit PlaceHolder="{Loc 'ui-search'}" HorizontalExpand="True" />`` |
| `Button` | Primary user action | ``<Button Text="{Loc 'ui-confirm'}" />`` |
| `TextureButton` | Button icon (help/close/refresh) | ``<TextureButton StyleClasses="windowCloseButton" />`` |
| `ItemList` | Selecting from a list of elements | ``<ItemList Name="MusicList" SelectMode="Button" VerticalExpand="True" />`` |
| `Slider` | Selecting a value by range | ``<Slider Name="PlaybackSlider" HorizontalExpand="True" />`` |
| `TextureRect` | Displaying textures/icons | ``<TextureRect Stretch="KeepCentered" />`` |

## Quick approach selection

1. Do you need a standard game window chrome, header and uniform style?
- Use `FancyWindow`.
2. Need a reusable compound element?
- Do `Control` + XAML + `[GenerateTypedNameReferences]`.
3. Do you need dynamic elements (buttons/filters/lists) after loading the markup?
- Add them in the constructor after `RobustXamlLoader.Load(this)`.

## Working patterns

- Build layout through containers (`BoxContainer`, `ScrollContainer`, `GridContainer`), and not through manual coordinates.
- Keep XAML declarative: structure, classes, base attributes.
- Keep the behavior in the code-behind: subscriptions, data filling, conditional logic.
- Localize text using `Loc`/`{Loc ...}` and do not hardcode strings.
- Use `Name` + `[GenerateTypedNameReferences]` to get type-safe references to controls.
- Add `Access="Public"` only where the element is really needed outside.

## Anti-patterns

- Position the UI in “pixels” when there are enough containers.
- Keep business logic directly in XAML.
- Bulk mark `Access="Public"` elements for no reason.
- Hardcode colors/strings instead of style classes and localization.
- Copy old examples without checking the date and problematic comments ⚠️

## Code examples

### Example 1: window frame via `FancyWindow`

```xml
<controls:FancyWindow xmlns="https://spacestation14.io"
                      xmlns:controls="clr-namespace:Content.Client.UserInterface.Controls"
                      MouseFilter="Stop"
                      MinWidth="200" MinHeight="150">
    <!-- Background panel using the current theme style -->
    <PanelContainer StyleClasses="BackgroundPanel" />

    <BoxContainer Orientation="Vertical">
        <Control>
            <!-- Window header with a style class -->
            <PanelContainer StyleClasses="WindowHeadingBackground" Name="WindowHeader" />
            <BoxContainer Margin="4 2 4 0" Orientation="Horizontal">
                <Label Name="WindowTitle"
                       HorizontalExpand="True" VAlign="Center" StyleClasses="FancyWindowTitle" />
                <TextureButton Name="CloseButton" StyleClasses="windowCloseButton" />
            </BoxContainer>
        </Control>

        <PanelContainer StyleClasses="LowDivider" />

        <!-- Public content container for later population -->
        <Control Access="Public" Name="ContentsContainer" VerticalExpand="true" />
    </BoxContainer>
</controls:FancyWindow>
```

### Example 2: Type-safe references + UI plugin

```csharp
[GenerateTypedNameReferences]
public sealed partial class ActionsWindow : DefaultWindow
{
    public MultiselectOptionButton<Filters> FilterButton { get; private set; }

    public ActionsWindow()
    {
        // First we load the XAML tree.
        RobustXamlLoader.Load(this);

        // Then we supplement it with dynamic control.
        SearchContainer.AddChild(FilterButton = new MultiselectOptionButton<Filters>
        {
            Label = Loc.GetString("ui-actionmenu-filter-button")
        });
    }
}
```

### Example 3: Multiple style classes in XAML

```xml
<Button Name="DoneButton" Text="{Loc 'nano-task-ui-done'}">
    <Button.StyleClasses>
        <!-- Combine button size and shape -->
        <system:String>ButtonSmall</system:String>
        <system:String>OpenLeft</system:String>
    </Button.StyleClasses>
</Button>
```

### Example 4: Data and event binding after markup is loaded

```csharp
public NanoTaskItemControl(NanoTaskItemAndId item)
{
    RobustXamlLoader.Load(this);

    // Filling the UI with data.
    TaskLabel.Text = item.Data.Description;
    TaskForLabel.Text = item.Data.TaskIsFor;

    // We tie user actions to domain logic.
    MainButton.OnPressed += _ => OnMainPressed?.Invoke(item.Id);
    DoneButton.OnPressed += _ => OnDonePressed?.Invoke(item.Id);
}
```

## Checklist before PR

- The window is assembled by containers and stretches correctly when resizing.
- All custom strings are localized.
- For static values, `x:Static`/`x:Type` are used, not duplicate literals.
- A strict connection between the class name and the name `.xaml` has been maintained.
- There is no visual hardcode, which should be in the style system.
- Subscriptions to events are meaningful, without “extra noise.”
- Only fresh and clean (without TODO/FIXME) code references are used 👍
