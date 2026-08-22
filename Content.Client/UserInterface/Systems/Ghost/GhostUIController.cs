using Content.Client.Gameplay;
using Content.Client.Ghost;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared.Ghost;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Content.Client._Sunrise.ServersHub;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Configuration;
using Content.Shared._Sunrise.NewLife;
using GhostWarpsResponseEvent = Content.Shared.Ghost.SharedGhostSystem.GhostWarpsResponseEvent;

namespace Content.Client.UserInterface.Systems.Ghost;

// TODO hud refactor BEFORE MERGE fix ghost gui being too far up
public sealed partial class GhostUIController : UIController, IOnSystemChanged<GhostSystem>
{
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private ServersHubManager _serversHubManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    private ISharedSponsorsManager? _sponsorsManager; // Sunrise-Sponsors

    [UISystemDependency] private readonly GhostSystem? _system = default;

    private GhostGui? Gui => UIManager.GetActiveUIWidgetOrNull<GhostGui>();

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;

        IoCManager.Instance!.TryResolveType(out _sponsorsManager); // Sunrise-Sponsors
    }

    private void OnScreenLoad()
    {
        LoadGui();
    }

    private void OnScreenUnload()
    {
        UnloadGui();
    }

    public void OnSystemLoaded(GhostSystem system)
    {
        system.PlayerRemoved += OnPlayerRemoved;
        system.PlayerUpdated += OnPlayerUpdated;
        system.PlayerAttached += OnPlayerAttached;
        system.PlayerDetached += OnPlayerDetached;
        system.GhostWarpsResponse += OnWarpsResponse;
        system.GhostRoleCountUpdated += OnRoleCountUpdated;
    }

    public void OnSystemUnloaded(GhostSystem system)
    {
        system.PlayerRemoved -= OnPlayerRemoved;
        system.PlayerUpdated -= OnPlayerUpdated;
        system.PlayerAttached -= OnPlayerAttached;
        system.PlayerDetached -= OnPlayerDetached;
        system.GhostWarpsResponse -= OnWarpsResponse;
        system.GhostRoleCountUpdated -= OnRoleCountUpdated;
    }

    public void UpdateGui()
    {
        if (Gui == null)
        {
            return;
        }

        Gui.Visible = _system?.IsGhost ?? false;

        // Sunrise-Start
        var newLifeEnable = _cfg.GetCVar(SunriseCCVars.NewLifeEnable);
        var canRespawn = false;
        if (newLifeEnable)
        {
            var sponsorOnly = _cfg.GetCVar(SunriseCCVars.NewLifeSponsorOnly);
            if (sponsorOnly && _sponsorsManager != null)
            {
                if (_sponsorsManager.ClientAllowedRespawn() || !sponsorOnly)
                {
                    canRespawn = true;
                }
                else
                {
                    canRespawn = false;
                }
            }
            else
            {
                canRespawn = true;
            }
        }
        // Sunrise-End

        Gui.Update(_system?.AvailableGhostRoleCount, _system?.Player?.CanReturnToBody, canRespawn);
    }

    private void OnPlayerRemoved(GhostComponent component)
    {
        Gui?.Hide();
    }

    private void OnPlayerUpdated(GhostComponent component)
    {
        UpdateGui();
    }

    private void OnPlayerAttached(GhostComponent component)
    {
        if (Gui == null)
            return;

        Gui.Visible = true;
        UpdateGui();
    }

    private void OnPlayerDetached()
    {
        Gui?.Hide();
    }

    private void OnWarpsResponse(GhostWarpsResponseEvent msg)
    {
        if (Gui?.TargetWindow is not { } window)
            return;

        window.UpdateWarps(msg.Players, msg.Places, msg.Antagonists);
        window.Populate();
    }

    private void OnRoleCountUpdated(GhostUpdateGhostRoleCountEvent msg)
    {
        UpdateGui();
    }

    private void OnWarpClicked(NetEntity player)
    {
        var msg = new GhostWarpToTargetRequestEvent(player);
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnGhostnadoClicked()
    {
        var msg = new GhostnadoRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    public void LoadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed += RequestWarps;
        Gui.ReturnToBodyPressed += ReturnToBody;
        Gui.GhostRolesPressed += GhostRolesPressed;
        Gui.RespawnPressed += Respawn; // Sunrise-Sponsors
        Gui.ChangeServerPressed += ChangeServerPressed;
        Gui.TargetWindow.WarpClicked += OnWarpClicked;

        // Sunrise edit - нету фичи
        // Gui.TargetWindow.OnGhostnadoClicked += OnGhostnadoClicked;

        UpdateGui();
    }

    public void UnloadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed -= RequestWarps;
        Gui.ReturnToBodyPressed -= ReturnToBody;
        Gui.GhostRolesPressed -= GhostRolesPressed;
        Gui.RespawnPressed -= Respawn; // Sunrise-Sponsors
        Gui.ChangeServerPressed -= ChangeServerPressed;
        Gui.TargetWindow.WarpClicked -= OnWarpClicked;

        Gui.Hide();
    }

    private void ReturnToBody()
    {
        _system?.ReturnToBody();
    }

    // Sunrise-Sponsors-Start
    private void Respawn()
    {
        var msg = new NewLifeOpenRequest();
        _net.SendSystemNetworkMessage(msg);
    }
    // Sunrise-Sponsors-End

    private void RequestWarps()
    {
        _system?.RequestWarps();
        Gui?.TargetWindow.Populate();
        Gui?.TargetWindow.OpenCentered();
    }

    private void GhostRolesPressed()
    {
        _system?.OpenGhostRoles();
    }

    private void ChangeServerPressed()
    {
        _serversHubManager.ToggleWindow();
    }
}
