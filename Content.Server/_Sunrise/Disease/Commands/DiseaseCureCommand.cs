using Content.Server.Administration;
using Content.Shared._Sunrise.Disease.Events;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Sunrise.Disease.Commands;

/// <summary>
/// Снимает с сущности одну или все болезни, не выдавая иммунитет.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class DiseaseCureCommand : BaseDiseaseCommand
{
    public override string Command => "diseasecure";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!TryParseEntity(shell, args[0], out var target))
            return;

        if (args.Length == 1)
        {
            if (Disease.GetDiseases(target).Count == 0)
            {
                shell.WriteError(Loc.GetString("cmd-diseasecure-not-infected"));
                return;
            }

            Disease.CureAll(target, DiseaseCureReason.Admin);
            shell.WriteLine(Loc.GetString("cmd-diseasecure-all-success"));
            return;
        }

        if (!TryParseDiseaseOf(shell, target, args[1], out var strain))
            return;

        if (!Disease.TryCure(target, strain, DiseaseCureReason.Admin))
        {
            shell.WriteError(Loc.GetString("cmd-diseasecure-not-infected"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-diseasecure-success", ("disease", DescribeDisease(strain))));
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
