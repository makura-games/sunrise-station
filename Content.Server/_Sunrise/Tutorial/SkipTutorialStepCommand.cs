using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Sunrise.Tutorial;

/// <summary>
/// Пропускает текущий шаг туториала для вызвавшего команду игрока.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class SkipTutorialStepCommand : LocalizedEntityCommands
{
    [Dependency] private TutorialSystem _tutorial = default!;

    public override string Command => "skiptutorialstep";
    public override string Description => "Skips your current tutorial step.";
    public override string Help => "Usage: skiptutorialstep";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        if (shell.Player == null)
        {
            shell.WriteError(Loc.GetString("shell-only-players-can-run-this-command"));
            return;
        }

        if (shell.Player.AttachedEntity is not { Valid: true } player)
        {
            shell.WriteError(Loc.GetString("shell-must-be-attached-to-entity"));
            return;
        }

        if (!_tutorial.TrySkipCurrentStep(player))
        {
            shell.WriteError("There is no active tutorial step to skip.");
            return;
        }

        shell.WriteLine("Current tutorial step skipped.");
    }
}
