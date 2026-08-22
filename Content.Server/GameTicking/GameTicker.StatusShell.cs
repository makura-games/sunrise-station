using System.Linq;
using System.Text.Json.Nodes;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Sunrise.Interfaces.Server;
using Robust.Server.ServerStatus;
using Robust.Shared.Configuration;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        /// <summary>
        ///     Used for thread safety, given <see cref="IStatusHost.OnStatusRequest"/> is called from another thread.
        /// </summary>
        private readonly object _statusShellLock = new();

        /// <summary>
        ///     Round start time in UTC, for status shell purposes.
        /// </summary>
        [ViewVariables]
        private DateTime _roundStartDateTime;

        /// <summary>
        ///     For access to CVars in status responses.
        /// </summary>
        [Dependency] private IConfigurationManager _cfg = default!;
        /// <summary>
        ///     For access to the round ID in status responses.
        /// </summary>
        [Dependency] private SharedGameTicker _gameTicker = default!;

        private void InitializeStatusShell()
        {
            IoCManager.Resolve<IStatusHost>().OnStatusRequest += GetStatusResponse;
        }

        private void GetStatusResponse(JsonNode jObject)
        {
            var preset = CurrentPreset ?? Preset;

            // This method is raised from another thread, so this better be thread safe!
            lock (_statusShellLock)
            {
                // Sunrise-Start
                var players = IoCManager.Instance?.TryResolveType<IServerJoinQueueManager>(out var joinQueueManager) ?? false
                    ? joinQueueManager.ActualPlayersCount
                    : _playerManager.PlayerCount;
                // Sunrise-End

                if (_cfg.GetCVar(CCVars.AdminsCountInReportedPlayerCount))
                {
                    players -= _adminManager.ActiveAdmins.Count();
                }

                jObject["name"] = _baseServer.ServerName;
                jObject["map"] = _gameMapManager.GetSelectedMap()?.MapName;
                jObject["round_id"] = _gameTicker.RoundId;
                jObject["players"] = players; // Sunrise-Queue
                jObject["soft_max_players"] = _cfg.GetCVar(CCVars.SoftMaxPlayers);
                jObject["panic_bunker"] = _cfg.GetCVar(CCVars.PanicBunkerEnabled);
                jObject["run_level"] = (int) _runLevel;
                if (preset != null)
                {
                    if (preset.Hide)
                        jObject["preset"] = Loc.GetString("gamemode-title-hide");
                    else
                        jObject["preset"] = (Decoy == null) ? Loc.GetString(preset.ModeTitle) : Loc.GetString(Decoy.ModeTitle);
                }
                if (_runLevel >= GameRunLevel.InRound)
                {
                    jObject["round_start_time"] = _roundStartDateTime.ToString("o");
                }
                jObject["short_name"] = _cfg.GetCVar(SunriseCCVars.ServersHubShortName); // Sunrise-Edit
            }
        }
    }
}
