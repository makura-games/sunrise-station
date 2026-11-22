using Content.Server.Administration;
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server._Sunrise.AssaultOps.Icarus.Commands;

[UsedImplicitly]
[AdminCommand(AdminFlags.Fun)]
public sealed partial class SpawnIcarusCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entityManager = default!;

    public string Command => "spawnicarus";
    public string Description => "Spawn Icarus beam and direct to specified grid center.";
    public string Help => "spawnicarus <gridId>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Incorrect number of arguments. " + Help);
            return;
        }

        if (!EntityUid.TryParse(args[0], out var uid))
        {
            shell.WriteError("Not a valid entity ID.");
            return;
        }

        if (!_entityManager.EntityExists(uid))
        {
            shell.WriteError("That grid does not exist.");
            return;
        }

        if (_entityManager.TryGetComponent<MapGridComponent>(uid, out var grid) &&
            _entityManager.TryGetComponent<TransformComponent>(uid, out var xform))
        {
            var icarusSystem = _entityManager.System<IcarusTerminalSystem>();
            var coords = icarusSystem.FireBeam(xform.WorldMatrix.TransformBox(grid.LocalAABB));
            shell.WriteLine($"Icarus was spawned: {coords.ToString()}");
        }
        else
        {
            shell.WriteError($"No grid exists with ID {uid}");
        }
    }
}
