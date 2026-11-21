---
name: ss14-ui-bui
description: A practical guide to Bound User Interface (BUI) in SS14: architecture, network messages, input validation, prediction through component state, lifecycle windows and server-client working patterns. Use it when developing and refactoring entity-bound interfaces.
---

# Bound User Interface (BUI) in SS14

This skill only covers BUI (entity-bound UI) :)
Manage XAML layout, EUI and style system in separate skills.

## When to choose BUI

Choose BUI when the interface:

- strictly tied to a specific entity;
- must open/close upon interaction with this entity;
- exchanges typed messages with the server logic of the same entity.

Don't choose BUI when you need a global admin/session interface without being tied to an entity (this is an EUI).

## BUI mental model

BUI consists of 4 layers:

1. `UiKey` + typed messages (`BoundUserInterfaceMessage`) + optional `BoundUserInterfaceState` in shared.
2. Prototypical interface registration (`UserInterface.interfaces`) and `ActivatableUI.key`.
3. Server-side processing of events/messages via `SharedUserInterfaceSystem` and the feature system.
4. Client `BoundUserInterface`, which creates a window, sends messages and updates the UI.

## Basic contract (shared)

```csharp
// Specific BUI key.
[NetSerializable, Serializable]
public enum BatteryUiKey : byte
{
    Key,
}

// Client message to server (button/switch).
[Serializable, NetSerializable]
public sealed class BatterySetInputBreakerMessage(bool on) : BoundUserInterfaceMessage
{
    public bool On = on;
}

// Full UI-state (if you really need a separate BUI-state).
[Serializable, NetSerializable]
public sealed class BatteryBuiState : BoundUserInterfaceState
{
    public bool CanCharge;
    public float Charge;
    public float Capacity;
}
```

Pattern:
- make messages narrow and specific (`SetX`, `ToggleY`), not “universal”.

Anti-pattern:
- one “all-in-one” message with dozens of nullable fields.

## Binding in prototype

```yaml
# Keychain and client-side BUI class
- type: UserInterface
  interfaces:
    enum.GasVolumePumpUiKey.Key:
      type: GasVolumePumpBoundUserInterface

# How does the player open this key?
- type: ActivatableUI
  key: enum.GasVolumePumpUiKey.Key
```

Pattern:
- `ActivatableUI.key` and the key in `UserInterface.interfaces` must always match.

Anti-pattern:
- register `UserInterface`, but forget `ActivatableUI` (or vice versa).

## Server-side processing: correct scheme

Use `Subs.BuiEvents<TComp>(uiKey, ...)` to subscribe to BUI events and messages.

```csharp
public override void Initialize()
{
    base.Initialize();

    Subs.BuiEvents<BatteryInterfaceComponent>(BatteryUiKey.Key, subs =>
    {
        // User messages.
        subs.Event<BatterySetInputBreakerMessage>(HandleSetInputBreaker);
        subs.Event<BatterySetOutputBreakerMessage>(HandleSetOutputBreaker);
    });
}

private void HandleSetInputBreaker(Entity<BatteryInterfaceComponent> ent, ref BatterySetInputBreakerMessage args)
{
    var netBattery = Comp<PowerNetworkBatteryComponent>(ent);
    netBattery.CanCharge = args.On; // Changing the domain model
}
```

Recommendation:
- update the UI only when the interface is open (`IsUiOpen`) and only when the data actually changes.

## Client-side BUI: lifecycle

```csharp
public sealed class GasVolumePumpBoundUserInterface : BoundUserInterface
{
    private GasVolumePumpWindow? _window;

    protected override void Open()
    {
        base.Open();

        // Helper creates a window, opens it, binds Close -> bui.Close(), registers the position.
        _window = this.CreateWindow<GasVolumePumpWindow>();

        _window.ToggleStatusButtonPressed += OnToggle;
        _window.PumpTransferRateChanged += OnRateChanged;
        Update();
    }

    private void OnToggle()
    {
        if (_window == null)
            return;

        SendPredictedMessage(new GasVolumePumpToggleStatusMessage(_window.PumpStatus));
    }
}
```

Pattern:
- subscribe UI elements in `Open()`, and not in the constructor.

Anti-pattern:
- direct `new Window().OpenCentered()` without `CreateWindow<T>()` and without linking the closure to the BUI.

## Prediction: modern way (preferred)

Priority approach: the UI already reads the network fields of the component, and does not duplicate everything in `BoundUserInterfaceState`.

### 1) The component raises `AfterAutoHandleStateEvent`

```csharp
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class GasVolumePumpComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public float TransferRate = Atmospherics.MaxTransferRate;
}
```

### 2) The client system updates the open BUI from the component state

```csharp
public override void Initialize()
{
    base.Initialize();
    SubscribeLocalEvent<GasVolumePumpComponent, AfterAutoHandleStateEvent>(OnPumpState);
}

private void OnPumpState(Entity<GasVolumePumpComponent> ent, ref AfterAutoHandleStateEvent args)
{
    if (_ui.TryGetOpenUi(ent.Owner, GasVolumePumpUiKey.Key, out var bui))
        bui.Update(); // UI reads the actual component
}
```

### 3) BUI sends input via `SendPredictedMessage`

```csharp
private void OnPumpTransferRatePressed(string value)
{
    var rate = UserInputParser.TryFloat(value, out var parsed) ? parsed : 0f;
    SendPredictedMessage(new GasVolumePumpChangeTransferRateMessage(rate));
}
```

## When you need `BoundUserInterfaceState`

Use `SetUiState(...)` if:

- the state is difficult/expensive to collect on the client from existing component fields;
- you need a server-authoritative snapshot of a complex aggregated model;
- you need to distribute the state only to the actors who opened the interface.

If everything you need is already `AutoNetworkedField`, prefer the component path.

## High frequency input (sliders, etc.)

For “noisy” input, use the following link:

- `InputCoalescer<T>` for gluing together multiple UI events;
- `IBuiPreTickUpdate` + `BuiPreTickUpdateSystem` for sending no more than once per tick;
- `BuiPredictionState` to re-overlay unacknowledged client messages onto the incoming server state.

```csharp
void IBuiPreTickUpdate.PreTickUpdate()
{
    if (_chargeRateCoalescer.CheckIsModified(out var chargeRateValue))
        _pred!.SendMessage(new BatterySetChargeRateMessage(chargeRateValue));
}
```

Pattern:
- coalesce the values ​​of the sliders and send them in batches to tick.

Anti-pattern:
- send `SendMessage` for each pixel-move of the slider.

## Message security and validation

The default is `InterfaceData.RequireInputValidation = true`, and that's correct ✅

For each incoming BUI message, the engine raises `BoundUserInterfaceMessageAttempt`, where the following is checked:

- possibility of interaction (`CanInteract`, `CanComplexInteract`);
- accessibility/distance;
- single-user restrictions and other domain rules.

Disable `RequireInputValidation` only in strictly justified cases.

## Distance and auto-close

`SharedUserInterfaceSystem` does a range-check of open interfaces and closes the BUI when leaving the radius.

Manage this through `InterfaceData.InteractionRange` and, if necessary, through `BoundUserInterfaceCheckRangeEvent`.

Pattern:
- set a reasonable `InteractionRange` for physically close devices.

Anti-pattern:
- set a large range for no reason and get “remote control of everything”.

## Critical patterns

- Keep `UiKey`/messages/state in shared and make it strictly typed.
- UI buttons raise messages; business logic lives in the system, not in the window.
- For prediction, first try component-state + `AfterAutoHandleStateEvent`.
- Update the UI only if it is actually open (`TryGetOpenUi`).
- Use `this.CreateWindow<T>()` for the correct lifecycle window.
- Group subscriptions to BUI events using `Subs.BuiEvents`.

## Anti-patterns

- Duplicate the same data in both the component and the large BUI-state for no reason.
- Directly change components from the window/control, bypassing messages.
- Spam `SetUiState` every frame without guard change checks.
- Ignore input validation and range restrictions.
- Mix BUI and EUI in the same scenario for no architectural reason.

## Checklist before PR

- The UI key is registered in both `UserInterface` and `ActivatableUI`.
- `BoundUserInterfaceMessage` messages are minimal and domain-driven.
- The server system validates and processes messages through BUI-events.
- The client BUI does not contain business logic, only displaying and sending input.
- For prediction, `SendPredictedMessage` is used where necessary.
- There is no unnecessary network duplication of state data.
