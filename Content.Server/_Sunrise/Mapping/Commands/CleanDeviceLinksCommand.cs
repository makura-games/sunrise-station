using Content.Server.Administration;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.Administration;
using Content.Shared.DeviceLinking;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server._Sunrise.Mapping.Commands;

/// <summary>
/// Removes invalid saved device-link references from a map before it is exported or fixed manually.
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed partial class CleanDeviceLinksCommand : LocalizedEntityCommands
{
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private MapSystem _map = default!;

    public override string Command => "cleandevicelinks";

    /// <summary>
    /// Cleans invalid saved device-link references for the requested map.
    /// </summary>
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!TryParseMapId(shell, args, out var mapId))
            return;

        if (!CanCleanupDeviceLinksForMap(shell, mapId))
            return;

        var result = DoCleanupDeviceLinksForMap(mapId);
        shell.WriteLine(Loc.GetString(
            "cmd-cleandevicelinks-cleaned",
            ("mapId", mapId),
            ("removedSinkEntries", result.RemovedSinkEntries),
            ("removedLinkPairs", result.RemovedLinkPairs),
            ("affectedSources", result.AffectedSources)));
    }

    private bool TryParseMapId(IConsoleShell shell, string[] args, out MapId mapId)
    {
        mapId = MapId.Nullspace;

        switch (args.Length)
        {
            case 0:
                var player = shell.Player;
                if (player == null)
                {
                    shell.WriteError(Loc.GetString("shell-only-players-can-run-this-command"));
                    return false;
                }

                if (player.AttachedEntity is not { Valid: true } playerEntity)
                {
                    shell.WriteError(Loc.GetString("shell-must-be-attached-to-entity"));
                    return false;
                }

                mapId = EntityManager.GetComponent<TransformComponent>(playerEntity).MapID;
                return true;
            case 1:
                if (!int.TryParse(args[0], out var intMapId))
                {
                    shell.WriteError(Loc.GetString("cmd-parse-failure-mapid", ("arg", args[0])));
                    return false;
                }

                mapId = new MapId(intMapId);
                return true;
            default:
                shell.WriteLine(Help);
                return false;
        }
    }

    private bool CanCleanupDeviceLinksForMap(IConsoleShell shell, MapId mapId)
    {
        if (mapId == MapId.Nullspace || !_map.MapExists(mapId))
        {
            shell.WriteError(Loc.GetString("cmd-cleandevicelinks-map-missing", ("mapId", mapId)));
            return false;
        }

        return true;
    }

    private DeviceLinkSaveCleanupResult DoCleanupDeviceLinksForMap(MapId mapId)
    {
        return _deviceLink.CleanupLinksForMapSave(mapId);
    }

    /// <summary>
    /// Provides map-id completion for the command.
    /// </summary>
    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(
                CompletionHelper.MapIds(EntityManager),
                Loc.GetString("cmd-cleandevicelinks-hint-map"))
            : CompletionResult.Empty;
    }
}
