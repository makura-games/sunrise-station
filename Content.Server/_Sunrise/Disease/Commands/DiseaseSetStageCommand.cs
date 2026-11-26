using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Sunrise.Disease.Commands;

/// <summary>
/// Переводит болезнь сущности на другую стадию. Для админов стадии нумеруются с единицы.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed partial class DiseaseSetStageCommand : BaseDiseaseCommand
{
    public override string Command => "diseasesetstage";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!TryParseEntity(shell, args[0], out var target) || !TryParseDiseaseOf(shell, target, args[1], out var strain))
            return;

        if (!int.TryParse(args[2], out var stage))
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-number"));
            return;
        }

        if (!Disease.TrySetStage(target, strain, stage - 1))
        {
            shell.WriteError(Loc.GetString("cmd-diseasesetstage-failed",
                ("disease", DescribeDisease(strain)),
                ("count", Registry.TryGetData(strain, out var data) ? data.Stages.Count : 0)));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-diseasesetstage-success",
            ("disease", DescribeDisease(strain)),
            ("stage", stage)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => EntityCompletion(args[0]),
            2 => DiseaseCompletion(),
            3 => CompletionResult.FromHint(Loc.GetString("cmd-disease-hint-stage")),
            _ => CompletionResult.Empty,
        };
    }
}
