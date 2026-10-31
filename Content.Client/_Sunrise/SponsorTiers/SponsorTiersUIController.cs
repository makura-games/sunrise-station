// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt
using Content.Client.Lobby;
using Content.Client._Sunrise.Lobby;
using Content.Sunrise.Interfaces.Shared;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Sunrise.SponsorTiers;

public sealed partial class SponsorTiersUIController : UIController, IOnStateEntered<SunriseLobbyState>
{
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    private SponsorTiersUi _sponsorTiersUi = default!;
    private bool _shown;

    public void OnStateEntered(SunriseLobbyState state)
    {
        IoCManager.Instance!.TryResolveType<ISharedSponsorsManager>(out var sponsors);

        if (_shown || sponsors == null || sponsors.ClientIsSponsor())
            return;

        ToggleWindow();
        _shown = true;
    }

    public void OpenWindow()
    {
        EnsureWindow();

        _sponsorTiersUi.OpenCentered();
        _sponsorTiersUi.MoveToFront();
    }

    private void EnsureWindow()
    {
        if (_sponsorTiersUi is { Disposed: false })
            return;

        _sponsorTiersUi = _uiManager.CreateWindow<SponsorTiersUi>();
    }

    public void ToggleWindow()
    {
        EnsureWindow();

        if (_sponsorTiersUi.IsOpen)
        {
            _sponsorTiersUi.Close();
        }
        else
        {
            OpenWindow();
        }
    }
}
