using Content.Server.Administration;
using Content.Server._Sunrise.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Shared.Console;

namespace Content.Server._Sunrise.Chat.Commands;

[UsedImplicitly]
[AdminCommand(AdminFlags.Debug)]
public sealed partial class SpawnArrivalsShuttleCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;

    public override string Command => "spawnarrivalsshuttle";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-must-be-attached-to-entity"));
            return;
        }

        var stationSystem = _entities.System<StationSystem>();
        if (stationSystem.GetOwningStation(player) is not { } station)
        {
            shell.WriteError(Loc.GetString("cmd-spawnarrivalsshuttle-not-on-station"));
            return;
        }

        if (!_entities.System<SunriseArrivalsSystem>().TrySpawnForPlayer(station, player))
        {
            shell.WriteError(Loc.GetString("cmd-spawnarrivalsshuttle-failed"));
            return;
        }

        shell.WriteLine(Loc.GetString("shell-command-success"));
    }
}
