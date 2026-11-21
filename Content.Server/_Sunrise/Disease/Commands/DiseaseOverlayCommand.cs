using Content.Server._Sunrise.Disease.Systems;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Sunrise.Disease.Commands;

/// <summary>
/// Переключает админский оверлей болезней: все заражённые предметы, облака и носители с их болезнями.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class DiseaseOverlayCommand : LocalizedEntityCommands
{
    [Dependency] private DiseaseOverlaySystem _overlay = default!;

    public override string Command => "diseaseoverlay";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-only-players-can-run-this-command"));
            return;
        }

        shell.WriteLine(Loc.GetString(_overlay.ToggleAdminOverlay(player)
            ? "cmd-diseaseoverlay-enabled"
            : "cmd-diseaseoverlay-disabled"));
    }
}
