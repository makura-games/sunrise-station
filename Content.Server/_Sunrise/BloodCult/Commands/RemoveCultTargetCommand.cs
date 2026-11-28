using Content.Server._Sunrise.BloodCult.GameRule;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Server.Player;

namespace Content.Server._Sunrise.BloodCult.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class RemoveCultTargetCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public string Command => "bloodcult_removetarget";
    public string Description => Loc.GetString("bloodcult-removetarget-description");
    public string Help => Loc.GetString("bloodcult-removetarget-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("bloodcult-removetarget-usage"));
            return;
        }

        var ckey = args[0];
        if (!_playerManager.TryGetSessionByUsername(ckey, out var session) || session.AttachedEntity == null)
        {
            shell.WriteError(Loc.GetString("bloodcult-removetarget-player-not-found", ("ckey", ckey)));
            return;
        }

        var entityUid = session.AttachedEntity.Value;

        if (!_entManager.EntitySysManager.TryGetEntitySystem<BloodCultRuleSystem>(out var cultRuleSystem))
        {
            shell.WriteError(Loc.GetString("bloodcult-removetarget-system-not-found"));
            return;
        }

        var rule = cultRuleSystem.GetRule();
        if (rule == null)
        {
            shell.WriteError(Loc.GetString("bloodcult-removetarget-rule-not-found"));
            return;
        }

        // Use the system method to remove target properly
        if (!cultRuleSystem.RemoveSpecificCultTarget(entityUid, rule))
        {
            shell.WriteError(Loc.GetString("bloodcult-removetarget-not-target"));
            return;
        }

        var targetName = _entManager.TryGetComponent<MetaDataComponent>(entityUid, out var meta)
            ? meta.EntityName
            : Loc.GetString("bloodcult-unknown-entity");
        shell.WriteLine(Loc.GetString("bloodcult-removetarget-success", ("name", targetName)));
    }
}
