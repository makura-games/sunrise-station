using Content.Server._Sunrise.Disease.Components;
using Content.Server._Sunrise.Disease.Systems;
using Content.Server.Administration;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Disease.Commands;

/// <summary>
/// Выводит список болезней и иммунитетов сущности.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class DiseaseListCommand : BaseDiseaseCommand
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DiseaseAnalysisSystem _analysis = default!;
    [Dependency] private DiseaseResearchSystem _research = default!;
    [Dependency] private DiseaseStrengthSystem _strength = default!;

    public override string Command => "diseaselist";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!TryParseEntity(shell, args[0], out var target))
            return;

        var diseases = Disease.GetDiseases(target);
        if (diseases.Count == 0)
            shell.WriteLine(Loc.GetString("cmd-diseaselist-none"));

        foreach (var instance in diseases)
        {
            if (!Registry.TryGetData(instance.Disease, out var disease))
                continue;

            shell.WriteLine(Loc.GetString("cmd-diseaselist-entry",
                ("disease", disease.Template?.Id ?? disease.StrainCode),
                ("strain", _analysis.GetStrainCode(instance.Disease)),
                ("stage", instance.Stage + 1),
                ("count", disease.Stages.Count),
                ("elapsed", (int) instance.StageElapsed.TotalSeconds),
                ("recovering", instance.Recovering ? "true" : "false"),
                ("suppression", (int) instance.SuppressionRemaining.TotalSeconds),
                ("cure", instance.CureProgress),
                ("threshold", disease.CureThreshold)));

            // Сила штамма и прогресс его исследования: по ним видно размер загадки с партиями вакцины.
            shell.WriteLine(Loc.GetString("cmd-diseaselist-research",
                ("strength", _strength.GetStrength(instance.Disease)),
                ("research", _research.GetResearch(instance.Disease)),
                ("goal", _research.GetResearchGoal(instance.Disease)),
                ("solved", _research.IsSolved(instance.Disease) ? "true" : "false")));

            if (instance.Symptoms.Count == 0)
            {
                shell.WriteLine(Loc.GetString("cmd-diseaselist-symptoms-none"));
                continue;
            }

            var symptoms = new List<string>(instance.Symptoms.Count);
            foreach (var active in instance.Symptoms)
            {
                symptoms.Add($"{active.Symptom.Id} ({active.Stage + 1})");
            }

            shell.WriteLine(Loc.GetString("cmd-diseaselist-symptoms", ("symptoms", string.Join(", ", symptoms))));
        }

        if (!EntityManager.TryGetComponent<DiseaseImmunityComponent>(target, out var immunity))
            return;

        var curTime = _timing.CurTime;
        foreach (var (disease, expiresAt) in immunity.Immunities)
        {
            // Иммунитет помнит штамм, поэтому в выводе он назван своим кодом, а не именем архетипа.
            var name = Registry.TryGetData(disease, out var data) ? data.StrainCode : disease.ToString();
            if (expiresAt == null)
            {
                shell.WriteLine(Loc.GetString("cmd-diseaselist-immunity-permanent", ("disease", name)));
                continue;
            }

            if (expiresAt <= curTime)
                continue;

            shell.WriteLine(Loc.GetString("cmd-diseaselist-immunity-temporary",
                ("disease", name),
                ("remaining", (int) (expiresAt.Value - curTime).TotalSeconds)));
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1 ? EntityCompletion(args[0]) : CompletionResult.Empty;
    }
}
