using System.Linq;
using Content.Server.Antag.Components;
using Content.Shared.Antag;
using Content.Shared.GameTicking;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using AntagPrototype = Content.Shared.Roles.AntagPrototype;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Antag;

public sealed partial class AntagSelectionSystem
{
    private const int SunrisePrimaryPreferencePriority = 1;
    private const int SunriseFallbackPreferencePriority = 0;

    private ISharedSponsorsManager? _sponsorsManager;

    private void InitializeSunriseAntagSelection()
    {
        IoCManager.Instance!.TryResolveType(out _sponsorsManager);
    }

    private static bool CanSelectSunriseLateJoinAntag(PlayerSpawnCompleteEvent args)
    {
        return args.CanBeAntag;
    }

    private static HashSet<string> GetSunrisePreferenceRoles(IEnumerable<AntagSpecifierPrototype> definitions)
    {
        var roles = new HashSet<string>();
        foreach (var definition in definitions)
        {
            foreach (var role in definition.PrefRoles)
                roles.Add(role.Id);
        }

        return roles;
    }

    private Dictionary<ICommonSession, float> GetSunriseWeightedPlayerPool(
        IEnumerable<ICommonSession> players,
        IEnumerable<AntagSpecifierPrototype> definitions)
    {
        var preferenceRoles = GetSunrisePreferenceRoles(definitions);
        var weightedPool = GetWeightedPlayerPool(players);
        foreach (var player in weightedPool.Keys.ToArray())
            weightedPool[player] = GetSunriseAntagWeight(player, preferenceRoles);

        return weightedPool;
    }

    private float GetSunriseAntagWeight(ICommonSession player, HashSet<string> preferenceRoles)
    {
        if (_sponsorsManager?.TryGetPriorityAntags(player.UserId, out var priorities) == true)
        {
            foreach (var priority in priorities)
            {
                if (preferenceRoles.Contains(priority))
                    return 2f;
            }
        }

        return 1f;
    }

    private bool HasSunriseAntagPreference(ICommonSession player, AntagSpecifierPrototype definition)
    {
        if (!ShouldCheckSunriseAntagPreference(definition))
            return true;

        return TryGetValidAntagPreferences(player, out var preferences) &&
               HasSunriseAntagPreference(preferences, definition);
    }

    private bool HasSunriseAntagPreference(
        List<ProtoId<AntagPrototype>> preferences,
        AntagSpecifierPrototype definition)
    {
        return !ShouldCheckSunriseAntagPreference(definition) ||
               PrefsContain(preferences, definition.PrefRoles) ||
               PrefsContain(preferences, definition.FallbackRoles);
    }

    /// <summary>
    /// Проверяет предпочтение для поэтапного назначения: сначала основное либо роль без требований,
    /// и только после этого резервное.
    /// </summary>
    private bool MatchesSunriseAntagPreference(
        bool hasPreferences,
        List<ProtoId<AntagPrototype>> preferences,
        AntagSpecifierPrototype definition,
        int priority)
    {
        if (!ShouldCheckSunriseAntagPreference(definition))
            return priority == SunrisePrimaryPreferencePriority;

        if (!hasPreferences)
            return false;

        return priority switch
        {
            SunrisePrimaryPreferencePriority => PrefsContain(preferences, definition.PrefRoles),
            SunriseFallbackPreferencePriority => PrefsContain(preferences, definition.FallbackRoles),
            _ => false
        };
    }

    private IEnumerable<int> GetSunriseAntagAssignmentOrder(
        bool hasPreferences,
        List<ProtoId<AntagPrototype>> preferences,
        IReadOnlyList<AntagRule> antags)
    {
        for (var priority = SunrisePrimaryPreferencePriority;
             priority >= SunriseFallbackPreferencePriority;
            priority--)
        {
            for (var i = antags.Count - 1; i >= 0; i--)
            {
                if (antags[i].Count <= 0)
                    continue;

                if (MatchesSunriseAntagPreference(hasPreferences, preferences, antags[i].Definition, priority))
                    yield return i;
            }
        }
    }

    private IEnumerable<int> GetSunriseAntagAssignmentOrder(
        bool hasPreferences,
        List<ProtoId<AntagPrototype>> preferences,
        IReadOnlyList<AntagCount> antags)
    {
        for (var priority = SunrisePrimaryPreferencePriority;
             priority >= SunriseFallbackPreferencePriority;
            priority--)
        {
            for (var i = antags.Count - 1; i >= 0; i--)
            {
                if (antags[i].Count <= 0)
                    continue;

                if (MatchesSunriseAntagPreference(hasPreferences, preferences, antags[i].Definition, priority))
                    yield return i;
            }
        }
    }

    private static bool ShouldCheckSunriseAntagPreference(AntagSpecifierPrototype definition)
    {
        return definition.PrefRoles.Count != 0 || definition.FallbackRoles.Count != 0;
    }

    private bool CanSelectSunriseCommandStaff(
        Entity<AntagSelectionComponent> gameRule,
        ICommonSession player,
        AntagSpecifierPrototype definition)
    {
        if (!_jobs.IsCommandStaff(player))
            return true;

        if (!definition.PickCommandStaff)
            return false;

        if (definition.MaxCommandStaff == 0)
            return true;

        var selected = new HashSet<ICommonSession>();
        foreach (var sessions in gameRule.Comp.PreSelectedSessions.Values)
            selected.UnionWith(sessions);

        var commandCount = 0;
        foreach (var selectedSession in selected)
        {
            if (_jobs.IsCommandStaff(selectedSession))
                commandCount++;
        }

        return commandCount < definition.MaxCommandStaff;
    }

    private void RaiseSunriseAntagSelectionComplete(Entity<AntagSelectionComponent> gameRule)
    {
        if (gameRule.Comp.SunriseSelectionComplete)
            return;

        gameRule.Comp.SunriseSelectionComplete = true;
        var ev = new AntagSelectionCompleteEvent(gameRule);
        RaiseLocalEvent(gameRule, ref ev, true);
    }

    private static HashSet<Entity<AntagSelectionComponent>> GetSunriseSelectionRules(IEnumerable<AntagRule> antags)
    {
        var rules = new HashSet<Entity<AntagSelectionComponent>>();
        foreach (var antag in antags)
            rules.Add(antag.GameRule);

        return rules;
    }

    private void RaiseSunriseAntagSelectionComplete(IEnumerable<Entity<AntagSelectionComponent>> gameRules)
    {
        foreach (var gameRule in gameRules)
            RaiseSunriseAntagSelectionComplete(gameRule);
    }
}
