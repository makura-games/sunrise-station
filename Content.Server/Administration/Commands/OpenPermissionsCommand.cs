using Content.Server.Administration.UI;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands
{
    [AdminCommand(AdminFlags.Permissions)]
    public sealed partial class OpenPermissionsCommand : LocalizedEntityCommands
    {
        [Dependency] private EuiManager _euiManager = default!;
        [Dependency] private IAdminManager _adminManager = default!;

        public override string Command => "permissions";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteLine(Loc.GetString($"shell-cannot-run-command-from-server"));
                return;
            }

            // Sunrise edit start - сервис Stellar Echoes является источником прав игрового сервера.
            if (SunriseAdminPermissionsGuard.IsBlocked(_adminManager, player))
            {
                shell.WriteLine("Права выдаются через Stellar Echoes.");
                return;
            }
            // Sunrise edit end

            var ui = new PermissionsEui();
            _euiManager.OpenEui(ui, player);
        }
    }
}
