using Content.Client._Sunrise.Sandbox.Transparency.Overlays;
using Content.Client.Administration.Managers;
using Content.Client.UserInterface.Systems.Sandbox;
using Content.Shared._Sunrise.Misc.Events;
using Content.Shared.Administration;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Sandbox.Transparency.Systems;

/// <summary>
/// Controls the mapping transparency overlay and its UI-facing settings.
/// </summary>
public sealed partial class MappingTransparencySystem : EntitySystem
{
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    /// <summary>
    /// Lowest transparency percentage accepted by the overlay controls.
    /// </summary>
    public const int MinTransparencyPercent = 5;

    /// <summary>
    /// Highest transparency percentage accepted by the overlay controls.
    /// </summary>
    public const int MaxTransparencyPercent = 90;

    /// <summary>
    /// Default transparency percentage used when the overlay is first enabled.
    /// </summary>
    public const int DefaultTransparencyPercent = 70;

    private MappingTransparencyOverlay? _overlay;

    /// <summary>
    /// Raised after the overlay state or transparency settings change.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Indicates whether the overlay is currently active.
    /// </summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// Indicates whether the current user may enable the overlay.
    /// </summary>
    public bool CanEnable => _admin.HasFlag(AdminFlags.Mapping);

    /// <summary>
    /// Gets the current transparency percentage applied by the overlay.
    /// </summary>
    public int TransparencyPercent { get; private set; } = DefaultTransparencyPercent;

    /// <summary>
    /// Creates the overlay instance and starts tracking admin permission changes.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        _admin.AdminStatusUpdated += OnAdminStatusUpdated;
        UpdateUi();

        SubscribeNetworkEvent<ToggleMappingTransparencyEvent>(OnToggle);
    }

    /// <summary>
    /// Removes the overlay, restores any cached transparency, and detaches listeners.
    /// </summary>
    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
        {
            _overlay.ResetTransparency();
            _overlayMan.RemoveOverlay(_overlay);
            _overlay.Dispose();
            _overlay = null;
        }

        _admin.AdminStatusUpdated -= OnAdminStatusUpdated;
    }

    private void OnAdminStatusUpdated()
    {
        if (Enabled && !CanSetEnabled(true))
            SetEnabled(false);
        else
        {
            UpdateUi();
            StateChanged?.Invoke();
        }
    }

    private void OnToggle(ToggleMappingTransparencyEvent ev, EntitySessionEventArgs args)
    {
        TrySetEnabled(!Enabled);
    }

    /// <summary>
    /// Attempts to enable or disable the overlay.
    /// </summary>
    public bool TrySetEnabled(bool enabled)
    {
        if (Enabled == enabled)
            return true;

        if (!CanSetEnabled(enabled))
            return false;

        SetEnabled(enabled);
        return true;
    }

    /// <summary>
    /// Returns whether the requested enabled state is currently allowed.
    /// </summary>
    public bool CanSetEnabled(bool enabled)
    {
        return !enabled || CanEnable;
    }

    /// <summary>
    /// Updates the overlay transparency percentage after clamping it to the supported range.
    /// </summary>
    public void SetTransparencyPercent(int percent)
    {
        var clamped = Math.Clamp(percent, MinTransparencyPercent, MaxTransparencyPercent);
        if (TransparencyPercent == clamped)
            return;

        TransparencyPercent = clamped;
        _overlay?.TransparencyPercent = clamped;
        StateChanged?.Invoke();
    }

    private void SetEnabled(bool enabled)
    {
        Enabled = enabled;

        if (enabled)
        {
            _overlay = new();
            _overlay.TransparencyPercent = TransparencyPercent;

            if (!_overlayMan.HasOverlay<MappingTransparencyOverlay>())
                _overlayMan.AddOverlay(_overlay);
        }
        else
        {
            _overlay?.ResetTransparency();

            if (_overlay != null)
                _overlayMan.RemoveOverlay(_overlay);
        }

        UpdateUi();
        StateChanged?.Invoke();
    }

    private void UpdateUi()
    {
        var controller = _ui.GetUIController<SandboxUIController>();
        controller.SetMappingTransparencyVisible(CanEnable);
        controller.SetToggleMappingTransparency(Enabled);
    }
}
