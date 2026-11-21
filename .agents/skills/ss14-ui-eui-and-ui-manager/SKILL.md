---
name: ss14-ui-eui-and-ui-manager
description: An in-depth practical guide to the combination of EUI, UserInterfaceSystem and UserInterfaceManager in SS14: choosing an approach, lifecycle, state/message exchange, window management via UIController and secure network patterns.
---

# EUI + UserInterfaceSystem + UserInterfaceManager

This skill only covers UI lifecycle architecture and networking for interfaces :)
Details of XAML layout and styling are provided in separate skills.

## Source of truth and selection of examples

1. Priority: current engine code and current game code.
2. Use the documentation from `docs` as a secondary source.
3. Do not use a code older than 2 years.
4. Do not use examples with TODO/FIXME/problematic comments on the UI grid and lifecycle.

## When to choose what

1. Local window/widget without server state?
- `UIController` + `UserInterfaceManager`.
2. Is the interface tied to the entity and its components?
- `BoundUserInterface` + `UserInterfaceSystem` (BUI).
3. Server-side dialog/control panel that doesn't have to live on the entity?
- `EUI` (server/client `BaseEui` + messages/status).

## Mental model

- `UserInterfaceManager` owns UI roots (`StateRoot`, `WindowRoot`, `PopupRoot`) and update queues.
- `UIController` lives as a singleton, connects input/state/system events and windows.
- BUI works with `BoundUserInterfaceMessage` messages and often opens `BaseWindow` through helpers.
- EUI works with `EuiMessageBase` messages and `EuiStateBase` state snapshots with an explicit `StateDirty`.

## Patterns

- Do `EnsureWindow()` and reuse the window instead of the uncontrolled `new`.
- In BUI, link `OnClose` windows with `bui.Close()` so that the client and server close synchronously.
- Transmit compact DTO state/message via EUI, without “raw” UI noise.
- Send updates via `StateDirty()`/queue rather than spamming them every tick.
- Subscribe/unsubscribe to state/system events at the appropriate lifecycle points.
- Share responsibility: the controller controls the flow, the window displays, the system stores the domain state.

## Anti-patterns

- Use EUI for high-frequency time-lapse telemetry.
- Create a new window for each click instead of reusing.
- Mix EUI and BUI for the same use-case for no reason.
- Forget to send `CloseEuiMessage` when closing the client-side window.
- Transmit “universal” weakly typed payloads into messages ⚠️

## Code examples

### Example 1: secure window lifecycle in `UIController`

```csharp
public sealed class OptionsUIController : UIController
{
    private OptionsMenu _optionsWindow = default!;

    private void EnsureWindow()
    {
        if (_optionsWindow is { Disposed: false })
            return;

        // Creation via UIManager, not direct new.
        _optionsWindow = UIManager.CreateWindow<OptionsMenu>();
    }

    public void ToggleWindow()
    {
        EnsureWindow();

        if (_optionsWindow.IsOpen)
            _optionsWindow.Close();
        else
            _optionsWindow.OpenCentered();
    }
}
```

### Example 2: entity-bound UI (BUI) with windowed view

```csharp
protected override void Open()
{
    base.Open();

    // We create a window as part of the lifecycle BUI.
    _menu = this.CreateWindow<JukeboxMenu>();

    _menu.OnPlayPressed += shouldPlay =>
    {
        // The client sends a typed message to the BUI server part.
        SendMessage(shouldPlay ? new JukeboxPlayingMessage() : new JukeboxPauseMessage());
    };

    _menu.OnStopPressed += () => SendMessage(new JukeboxStopMessage());
}
```

### Example 3: Restoring the window position for the BUI

```csharp
public static T CreateWindow<T>(this BoundUserInterface bui) where T : BaseWindow, new()
{
    var window = GetWindow<T>(bui);

    if (bui.EntMan.System<UserInterfaceSystem>().TryGetPosition(bui.Owner, bui.UiKey, out var position))
        window.Open(position);
    else
        window.OpenCentered();

    return window;
}
```

### Example 4: server-side EUI manager and deferred state sending

```csharp
public void QueueStateUpdate(BaseEui eui)
{
    _stateUpdateQueue.Enqueue((eui.Player, eui.Id));
}

public void SendUpdates()
{
    while (_stateUpdateQueue.TryDequeue(out var tuple))
    {
        var (player, id) = tuple;
        if (!_playerData.TryGetValue(player, out var plyDat) || !plyDat.OpenUIs.TryGetValue(id, out var ui))
            continue;

        // Real state sending in one place lifecycle.
        ui.DoStateUpdate();
    }
}
```

### Example 5: type-safe EUI state + client-side application

```csharp
[Serializable, NetSerializable]
public sealed class PlayerPanelEuiState(
    NetUserId guid,
    string username,
    TimeSpan playtime)
    : EuiStateBase
{
    public readonly NetUserId Guid = guid;
    public readonly string Username = username;
    public readonly TimeSpan Playtime = playtime;
}

public override void HandleState(EuiStateBase state)
{
    if (state is not PlayerPanelEuiState s)
        return;

    // The UI is updated from the state snapshot.
    PlayerPanel.SetUsername(s.Username);
    PlayerPanel.SetPlaytime(s.Playtime);
}
```

## Mini checklist

- Correct layer selected: `UIController` / BUI / EUI.
- The closing lifecycle is symmetrical to the opening lifecycle.
- Network messages are minimal and type safe.
- There are no duplicate windows and “dangling” subscriptions.
- The examples are based on fresh, non-problematic code ✅
