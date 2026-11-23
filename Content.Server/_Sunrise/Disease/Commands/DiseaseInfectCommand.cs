using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Sunrise.Disease.Commands;

/// <summary>
/// Заражает сущность болезнью в обход путей передачи, защиты и иммунитета.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed partial class DiseaseInfectCommand : BaseDiseaseCommand
{
    public override string Command => "diseaseinfect";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!TryParseEntity(shell, args[0], out var target) || !TryParseStrain(shell, args[1], out var strain))
            return;

        if (!Disease.Infect(target, strain))
        {
            shell.WriteError(Loc.GetString("cmd-diseaseinfect-failed", ("disease", args[1])));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-diseaseinfect-success", ("disease", DescribeDisease(strain))));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => EntityCompletion(args[0]),
            2 => DiseaseCompletion(),
            _ => CompletionResult.Empty,
        };
    }
}
