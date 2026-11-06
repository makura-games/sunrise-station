using Content.Client._Sunrise.Sandbox.DeviceLink.Systems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.UserInterface.Systems.Sandbox;

public sealed partial class SandboxUIController : IOnSystemChanged<DeviceLinkOverlaySystem>
{
    [UISystemDependency] private readonly DeviceLinkOverlaySystem _deviceLink = default!;

    private void InitializeDeviceLinkWindow()
    {
        if (_window == null)
            return;

        _window.ToggleDeviceLinkButton.OnPressed += _ =>
        {
            if (!_deviceLink.TrySetEnabled(!_deviceLink.Enabled))
                SyncDeviceLinkButton();
        };

        SyncDeviceLinkButton();
    }

    /// <summary>
    /// Updates the device-link toggle button state without firing its callbacks.
    /// </summary>
    public void SetToggleDeviceLink(bool value)
    {
        if (_window == null)
            return;

        _window.ToggleDeviceLinkButton.Pressed = value;
    }

    /// <summary>
    /// Shows or hides the device-link toggle button.
    /// </summary>
    public void SetDeviceLinkVisible(bool value)
    {
        if (_window == null)
            return;

        _window.ToggleDeviceLinkButton.Visible = value;
    }

    public void OnSystemLoaded(DeviceLinkOverlaySystem system)
    {
        system.StateChanged += SyncDeviceLinkButton;
        SyncDeviceLinkButton();
    }

    public void OnSystemUnloaded(DeviceLinkOverlaySystem system)
    {
        system.StateChanged -= SyncDeviceLinkButton;
        SetToggleDeviceLink(false);
        SetDeviceLinkVisible(false);
    }

    private void SyncDeviceLinkButton()
    {
        SetDeviceLinkVisible(_deviceLink.Enabled || _deviceLink.CanEnable);
        SetToggleDeviceLink(_deviceLink.Enabled);
    }
}
