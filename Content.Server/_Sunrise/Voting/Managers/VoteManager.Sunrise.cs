using System.Linq;
using Content.Server._Sunrise.Presets;
using Content.Server._Sunrise.Storyteller.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Voting.Managers;

public sealed partial class VoteManager
{
    [Dependency] private ILocalizationManager _loc = default!;

    private const string RegularVoteOptionId = "__regular__";
    private const string StorytellerVoteOptionId = "__storyteller__";

    /// <summary>
    /// Gets eligible non-Storyteller presets for the first stage of the Sunrise preset vote.
    /// </summary>
    /// <param name="excludedPresets">
    /// Preset IDs to omit. Each set item is a <see cref="GamePresetPrototype.ID"/> temporarily excluded by rotation;
    /// <see langword="null"/> leaves all configured regular presets eligible for consideration.
    /// </param>
    /// <returns>
    /// A dictionary whose key is a regular <see cref="GamePresetPrototype.ID"/> and whose value is its
    /// <see cref="GamePresetPrototype.ModeTitle"/> localization ID, not localized text.
    /// </returns>
    private Dictionary<string, string> GetSunriseRegularPresetsForVote(IReadOnlySet<string>? excludedPresets = null)
    {
        var ticker = _entityManager.System<GameTicker>();
        var presetPoolId = _cfg.GetCVar(SunriseCCVars.GamePresetPool);

        if (!_prototypeManager.TryIndex<GamePresetPoolPrototype>(presetPoolId, out var presetPoolProto))
            return new Dictionary<string, string>();

        var eligiblePresets = ticker.GetEligibleVotePresets(
            presetPoolProto.Presets,
            _playerManager.PlayerCount,
            excludedPresets);

        var result = new Dictionary<string, string>();

        foreach (var (presetId, title) in eligiblePresets)
        {
            if (!StorytellerSystem.IsStorytellerPreset(presetId))
                result[presetId] = title;
        }

        return result;
    }

    private (Dictionary<string, string> Regular, Dictionary<string, string> Storyteller, bool ResetExclusions)
        GetSunrisePresetVoteChoices()
    {
        var ticker = _entityManager.System<GameTicker>();
        var storyteller = _entityManager.System<StorytellerSystem>();

        var excludedPresets = ticker.ExcludedPresets.ToHashSet();
        var regularPresets = GetSunriseRegularPresetsForVote(excludedPresets);
        var storytellerPresets = storyteller.GetAvailableVotePresets(excludedPresets);

        var resetExclusions = false;
        if (regularPresets.Count == 0 && storytellerPresets.Count == 0 && excludedPresets.Count > 0)
        {
            regularPresets = GetSunriseRegularPresetsForVote();
            storytellerPresets = storyteller.GetAvailableVotePresets(new HashSet<string>());
            resetExclusions = true;
        }

        return (regularPresets, storytellerPresets, resetExclusions);
    }

    private bool CanCallSunrisePresetVote()
    {
        var (regularPresets, storytellerPresets, _) = GetSunrisePresetVoteChoices();
        var presetCount = regularPresets.Count + storytellerPresets.Count;

        if (presetCount == 0)
            return false;

        if (presetCount > 1)
            return true;

        var ticker = _entityManager.System<GameTicker>();
        var singlePresetId = regularPresets.Count == 1
            ? regularPresets.Keys.First()
            : storytellerPresets.Keys.First();

        return singlePresetId != ticker.Preset?.ID;
    }

    private bool TryCreateSunriseTwoStagePresetVote(ICommonSession? initiator)
    {
        var (regularPresets, storytellerPresets, resetExclusions) = GetSunrisePresetVoteChoices();

        if (resetExclusions)
            _entityManager.System<GameTicker>().ClearExcludedPresets();

        if (regularPresets.Count == 0 && storytellerPresets.Count == 0)
        {
            Logger.Warning("No suitable game modes for the current player count.");
            return true;
        }

        if (regularPresets.Count == 0)
        {
            _chatManager.DispatchServerAnnouncement(
                _loc.GetString("ui-vote-preset-category-auto-set",
                    ("category", _loc.GetString("ui-vote-storyteller-entry"))));
            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Preset category vote skipped, auto-selected: Storyteller");
            CreateSunriseStorytellerTypeVote(storytellerPresets, initiator);
            return true;
        }

        if (storytellerPresets.Count == 0)
        {
            _chatManager.DispatchServerAnnouncement(
                _loc.GetString("ui-vote-preset-category-auto-set",
                    ("category", _loc.GetString("ui-vote-preset-category-regular"))));
            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Preset category vote skipped, auto-selected: Regular");
            CreateSunriseRegularPresetVote(regularPresets, initiator);
            return true;
        }

        var options = CreateSunrisePresetVoteOptions(_loc.GetString("ui-vote-preset-category-title"), initiator);
        options.Options.Add((_loc.GetString("ui-vote-preset-category-regular"), RegularVoteOptionId));
        options.Options.Add((_loc.GetString("ui-vote-storyteller-entry"), StorytellerVoteOptionId));

        var vote = CreateVote(options);

        vote.OnFinished += (_, args) =>
        {
            string picked;
            if (args.Winner == null)
            {
                picked = (string) _random.Pick(args.Winners);
                _chatManager.DispatchServerAnnouncement(
                    _loc.GetString("ui-vote-preset-category-tie", ("category", GetPresetCategoryName(picked))));
            }
            else
            {
                picked = (string) args.Winner;
                _chatManager.DispatchServerAnnouncement(
                    _loc.GetString("ui-vote-preset-category-win", ("category", GetPresetCategoryName(picked))));
            }

            var loggedCategory = picked == StorytellerVoteOptionId ? "Storyteller" : "Regular";
            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Preset category vote finished: {loggedCategory}");

            if (picked == StorytellerVoteOptionId)
            {
                CreateSunriseStorytellerTypeVote(storytellerPresets, initiator);
                return;
            }

            CreateSunriseRegularPresetVote(regularPresets, initiator);
        };

        return true;
    }

    /// <summary>
    /// Starts the second vote stage using the regular preset snapshot captured before the first stage.
    /// </summary>
    /// <param name="presets">
    /// Available regular presets where each key is a preset ID and each value is its title localization ID.
    /// The collection is not recalculated if the player count changes during the first stage.
    /// </param>
    /// <param name="initiator">The player who started the vote, or <see langword="null"/> for a server vote.</param>
    private void CreateSunriseRegularPresetVote(
        IReadOnlyDictionary<string, string> presets,
        ICommonSession? initiator)
    {
        if (presets.Count == 1)
        {
            var singlePreset = presets.First();
            _chatManager.DispatchServerAnnouncement(
                _loc.GetString("ui-vote-gamemode-auto-set", ("preset", _loc.GetString(singlePreset.Value))));
            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Regular preset vote skipped, auto-selected: {singlePreset.Key}");
            _entityManager.System<GameTicker>().SetGamePreset(singlePreset.Key);
            return;
        }

        var options = CreateSunrisePresetVoteOptions(_loc.GetString("ui-vote-gamemode-title"), initiator);

        foreach (var (presetId, title) in presets)
        {
            options.Options.Add((_loc.GetString(title), presetId));
        }

        var vote = CreateVote(options);

        vote.OnFinished += (_, args) =>
        {
            string picked;
            if (args.Winner == null)
            {
                picked = (string) _random.Pick(args.Winners);
                _chatManager.DispatchServerAnnouncement(_loc.GetString("ui-vote-gamemode-tie"));
            }
            else
            {
                picked = (string) args.Winner;
                _chatManager.DispatchServerAnnouncement(_loc.GetString("ui-vote-gamemode-win"));
            }

            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Regular preset vote finished: {picked}");
            _entityManager.System<GameTicker>().SetGamePreset(picked);
        };
    }

    /// <summary>
    /// Starts the second vote stage using the Storyteller preset snapshot captured before the first stage.
    /// </summary>
    /// <param name="presets">
    /// Available Storyteller presets where each key is a preset ID and each value is its title localization ID.
    /// The collection is not recalculated if the player count changes during the first stage.
    /// </param>
    /// <param name="initiator">The player who started the vote, or <see langword="null"/> for a server vote.</param>
    private void CreateSunriseStorytellerTypeVote(
        IReadOnlyDictionary<string, string> presets,
        ICommonSession? initiator)
    {
        if (presets.Count == 1)
        {
            var singleSubtypeId = presets.Keys.First();
            var singleSubtypeName = _loc.GetString(StorytellerSystem.GetNameLocId(singleSubtypeId));

            _chatManager.DispatchServerAnnouncement(
                _loc.GetString("ui-vote-storyteller-auto-set", ("type", singleSubtypeName)));
            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Storyteller type vote skipped, auto-selected: {singleSubtypeId}");
            _entityManager.System<GameTicker>().SetGamePreset(singleSubtypeId);
            return;
        }

        var options = CreateSunrisePresetVoteOptions(_loc.GetString("ui-vote-storyteller-title"), initiator);

        foreach (var presetId in presets.Keys)
        {
            options.Options.Add((_loc.GetString(StorytellerSystem.GetVoteOptionLocId(presetId)), presetId));
        }

        var vote = CreateVote(options);

        vote.OnFinished += (_, args) =>
        {
            string picked;
            if (args.Winner == null)
            {
                picked = (string) _random.Pick(args.Winners);
                _chatManager.DispatchServerAnnouncement(
                    _loc.GetString("ui-vote-storyteller-type-tie",
                        ("type", _loc.GetString(StorytellerSystem.GetNameLocId(picked)))));
            }
            else
            {
                picked = (string) args.Winner;
                _chatManager.DispatchServerAnnouncement(
                    _loc.GetString("ui-vote-storyteller-type-win",
                        ("type", _loc.GetString(StorytellerSystem.GetNameLocId(picked)))));
            }

            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Storyteller type vote finished: {picked}");
            _entityManager.System<GameTicker>().SetGamePreset(picked);
        };
    }

    private string GetPresetCategoryName(string categoryId)
    {
        return _loc.GetString(categoryId == StorytellerVoteOptionId
            ? "ui-vote-storyteller-entry"
            : "ui-vote-preset-category-regular");
    }

    private VoteOptions CreateSunrisePresetVoteOptions(string title, ICommonSession? initiator)
    {
        var alone = _playerManager.PlayerCount == 1 && initiator != null;
        var options = new VoteOptions
        {
            Title = title,
            Duration = alone
                ? TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerAlone))
                : TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteTimerPreset)),
            DisplayVotes = _cfg.GetCVar(SunriseCCVars.ShowPresetVotes),
        };

        if (alone)
            options.InitiatorTimeout = TimeSpan.FromSeconds(10);

        WirePresetVoteInitiator(options, initiator);
        return options;
    }
}
