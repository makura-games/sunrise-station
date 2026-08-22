using Content.Server.Chat.Managers;
using Content.Server.Jobs;
using Content.Server.Preferences.Managers;
using Content.Server.Revolutionary.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.GameTicking.Rules;

public abstract partial class GameRuleSystem<T> where T : IComponent
{
    [Dependency] private IChatManager _sunriseChat = default!;
    [Dependency] private IComponentFactory _sunriseComponentFactory = default!;
    [Dependency] private IServerPreferencesManager _sunrisePreferences = default!;

    private void InitializeSunriseGameRule()
    {
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnSunriseStartAttempt);
    }

    private void OnSunriseStartAttempt(RoundStartAttemptEvent args)
    {
        if (args.Forced || args.Cancelled)
            return;

        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out _, out var gameRule))
        {
            if (gameRule.CancelPresetOnTooFewPlayers &&
                gameRule.MinCommandStaff > 0 &&
                !HasEnoughCommandCandidates(args, gameRule.MinCommandStaff))
            {
                _sunriseChat.SendAdminAnnouncement(Loc.GetString("preset-not-enough-ready-command-staff",
                    ("readyCommandStaffCount", args.Players.Length),
                    ("minimumCommandStaff", gameRule.MinCommandStaff),
                    ("presetName", ToPrettyString(uid))));
                args.Cancel();
                return;
            }

            if (args.Players.Length >= gameRule.MinPlayers)
                continue;

            var name = ToPrettyString(uid);
            if (gameRule.CancelPresetOnTooFewPlayers)
            {
                _sunriseChat.SendAdminAnnouncement(Loc.GetString("preset-not-enough-ready-players",
                    ("readyPlayersCount", args.Players.Length),
                    ("minimumPlayers", gameRule.MinPlayers),
                    ("presetName", name)));
                args.Cancel();
                Log.Info($"Rule '{name}' requires {gameRule.MinPlayers} players, but only {args.Players.Length} are ready.");
            }
            else
            {
                ForceEndSelf(uid, gameRule);
            }
        }
    }

    private bool HasEnoughCommandCandidates(RoundStartAttemptEvent args, int required)
    {
        var commandJobs = new HashSet<ProtoId<JobPrototype>>();

        foreach (var playerSession in args.Players)
        {
            if (_sunrisePreferences.GetPreferences(playerSession.UserId).SelectedCharacter is not HumanoidCharacterProfile profile)
                continue;

            foreach (var (jobId, priority) in profile.JobPriorities)
            {
                if (priority == JobPriority.Never ||
                    commandJobs.Contains(jobId) ||
                    !Proto.TryIndex<JobPrototype>(jobId, out var job))
                    continue;

                foreach (var special in job.Special)
                {
                    if (special is not AddComponentSpecial componentSpecial)
                        continue;

                    foreach (var component in componentSpecial.Components.Values)
                    {
                        if (_sunriseComponentFactory.GetComponent(component) is not CommandStaffComponent)
                            continue;

                        commandJobs.Add(jobId);
                        if (commandJobs.Count >= required)
                            return true;
                        break;
                    }
                }
            }
        }

        return false;
    }
}
