using Content.Client.Administration.Managers;
using Content.Shared._Sunrise.Sandbox;
using Robust.Client.Console;

namespace Content.Client._Sunrise.Sandbox.DeviceLink.Systems;

/// <summary>
/// Tracks the device-link debug overlay state for Sunrise sandbox UI wiring.
/// </summary>
public sealed partial class DeviceLinkOverlaySystem : EntitySystem
{
    /// <summary>
    /// Gets the console command that toggles the server-driven overlay.
    /// </summary>
    public const string ToggleCommand = "showdevicelink";

    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IClientConsoleHost _console = default!;

    /// <summary>
    /// Raised after the overlay state or command availability changes.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Indicates whether the overlay is currently active for the local client.
    /// </summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// Indicates whether the local client may toggle the overlay.
    /// </summary>
    public bool CanEnable => _admin.CanCommand(ToggleCommand);

    /// <summary>
    /// Subscribes to overlay state updates and admin permission changes.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        _admin.AdminStatusUpdated += OnAdminStatusUpdated;
        SubscribeNetworkEvent<DeviceLinkOverlayToggledEvent>(OnOverlayToggled);

        InitializeVisualization();
    }

    /// <summary>
    /// Detaches listeners owned by the system.
    /// </summary>
    public override void Shutdown()
    {
        base.Shutdown();

        _admin.AdminStatusUpdated -= OnAdminStatusUpdated;
        ShutdownVisualization();
    }

    /// <summary>
    /// Attempts to switch the overlay into the requested state.
    /// </summary>
    public bool TrySetEnabled(bool enabled)
    {
        if (Enabled == enabled)
            return true;

        if (enabled && !CanEnable)
            return false;

        _console.ExecuteCommand(ToggleCommand);
        return true;
    }

    private void OnAdminStatusUpdated()
    {
        StateChanged?.Invoke();
    }

    private void OnOverlayToggled(DeviceLinkOverlayToggledEvent args)
    {
        Enabled = args.IsEnabled;
        StateChanged?.Invoke();

        ToggleOverlay();
    }
}
