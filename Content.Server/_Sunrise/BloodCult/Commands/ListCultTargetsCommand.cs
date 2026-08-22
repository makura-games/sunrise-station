using Content.Server._Sunrise.BloodCult.GameRule;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Sunrise.BloodCult.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class ListCultTargetsCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;

    public string Command => "bloodcult_listtargets";
    public string Description => Loc.GetString("bloodcult-listtargets-description");
    public string Help => Loc.GetString("bloodcult-listtargets-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_entManager.EntitySysManager.TryGetEntitySystem<BloodCultRuleSystem>(out var cultRuleSystem))
        {
            shell.WriteError(Loc.GetString("bloodcult-listtargets-system-not-found"));
            return;
        }

        var rule = cultRuleSystem.GetRule();
        if (rule?.CultTargets == null || rule.CultTargets.Count == 0)
        {
            shell.WriteLine(Loc.GetString("bloodcult-listtargets-no-targets"));
            return;
        }

        shell.WriteLine(Loc.GetString("bloodcult-listtargets-header", ("count", rule.CultTargets.Count)));
        foreach (var (target, isSacrificed) in rule.CultTargets)
        {
            var targetName = _entManager.TryGetComponent<MetaDataComponent>(target, out var meta)
                ? meta.EntityName
                : Loc.GetString("bloodcult-unknown-entity");
            var status = isSacrificed ? Loc.GetString("bloodcult-listtargets-sacrificed") : Loc.GetString("bloodcult-listtargets-alive");
            shell.WriteLine(Loc.GetString("bloodcult-listtargets-target", ("name", targetName), ("uid", target), ("status", status)));
        }
    }
}
