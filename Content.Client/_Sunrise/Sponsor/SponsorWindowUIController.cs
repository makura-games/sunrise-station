using Content.Client._Sunrise.Sponsor;
using Content.Client.Lobby;
using Content.Sunrise.Interfaces.Shared;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindowUIController : UIController, IOnStateExited<LobbyState>
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    private SponsorWindow? _window;
    public void OnStateExited(LobbyState state)
    {
        _window?.Close();
    }

    public void OpenWindow()
    {
        var window = EnsureWindow();

        window.OpenCentered();
        window.MoveToFront();
    }

    public void ToggleWindow()
    {
        var window = EnsureWindow();

        if (window.IsOpen)
            window.Close();
        else
            OpenWindow();
    }

    private SponsorWindow EnsureWindow()
    {
        if (_window is { Disposed: false })
            return _window;

        _window = _ui.CreateWindow<SponsorWindow>();
        return _window;
    }
}
