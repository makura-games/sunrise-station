using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Sunrise.SponsorTiers;

public sealed partial class SponsorPersonalizationUIController : UIController
{
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    private SponsorPersonalizationUi? _window;

    public void OpenWindow()
    {
        EnsureWindow();

        _window!.OpenCentered();
        _window.MoveToFront();
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = _uiManager.CreateWindow<SponsorPersonalizationUi>();
    }

    public void ToggleWindow()
    {
        EnsureWindow();

        if (_window!.IsOpen)
        {
            _window.Close();
        }
        else
        {
            OpenWindow();
        }
    }
}
