using System.Linq;
using Content.Server.Administration;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.AlertLevel.Commands
{
    [AdminCommand(AdminFlags.Fun)]
    public sealed class SetAlertLevelCommand : LocalizedEntityCommands
    {
        [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
        [Dependency] private readonly StationSystem _stationSystem = default!;

        public override string Command => "setalertlevel";

        public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            var levelNames = new string[] {};
            var player = shell.Player;
            if (player?.AttachedEntity != null)
            {
                var stationUid = _stationSystem.GetOwningStation(player.AttachedEntity.Value);
                if (stationUid != null)
                    levelNames = GetStationLevelNames(stationUid.Value);
            }

            return args.Length switch
            {
                1 => CompletionResult.FromHintOptions(levelNames,
                    LocalizationManager.GetString("cmd-setalertlevel-hint-1")),
                2 => CompletionResult.FromHintOptions(CompletionHelper.Booleans,
                    LocalizationManager.GetString("cmd-setalertlevel-hint-2")),
                _ => CompletionResult.Empty,
            };
        }

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length is < 1 or > 2)
            {
                shell.WriteError(LocalizationManager.GetString("shell-wrong-arguments-number"));
                return;
            }

            var option = false;
            if (args.Length > 1 && !bool.TryParse(args[1], out option))
            {
                shell.WriteLine(LocalizationManager.GetString("shell-argument-must-be-boolean"));
                return;
            }

            var player = shell.Player;
            if (player?.AttachedEntity == null)
            {
                shell.WriteLine(LocalizationManager.GetString("shell-only-players-can-run-this-command"));
                return;
            }

            var stationUid = _stationSystem.GetOwningStation(player.AttachedEntity.Value);
            if (stationUid == null)
            {
                shell.WriteLine(LocalizationManager.GetString("cmd-setalertlevel-invalid-grid"));
                return;
            }

            var level = args[0];
            if (!EntityManager.TryGetComponent<AlertLevelComponent>(stationUid.Value, out var alertLevelComp)
                || alertLevelComp.AlertLevels == null
                || !alertLevelComp.AlertLevels.Levels.TryGetValue(level, out var detail))
            {
                shell.WriteLine(LocalizationManager.GetString("cmd-setalertlevel-invalid-level"));
                return;
            }

            // Sunrise edit start - второй параметр управляет состоянием дополнительного кода
            if (detail.IsAdditional)
            {
                var enabled = args.Length == 1 || option;
                if (!_alertLevelSystem.TrySetAdditionalLevel(
                        stationUid.Value,
                        level,
                        enabled,
                        true,
                        true,
                        true,
                        alertLevelComp))
                {
                    shell.WriteError(LocalizationManager.GetString("cmd-setalertlevel-change-failed"));
                }

                return;
            }

            _alertLevelSystem.SetLevel(stationUid.Value, level, true, true, true, option);
            // Sunrise edit end
        }

        private string[] GetStationLevelNames(EntityUid station)
        {
            if (!EntityManager.TryGetComponent<AlertLevelComponent>(station, out var alertLevelComp))
                return new string[]{};

            if (alertLevelComp.AlertLevels == null)
                return new string[]{};

            return alertLevelComp.AlertLevels.Levels.Keys.ToArray();
        }
    }
}
