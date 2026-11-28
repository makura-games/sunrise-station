using System.Linq;
using Content.Server._Sunrise.Presets;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared.Destructible.Thresholds;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Storyteller.Systems;

/// <summary>
/// Provides Storyteller preset pools and vote-facing metadata.
/// </summary>
public sealed partial class StorytellerSystem
{
    public const string StorytellerClassicId = "StorytellerClassic";
    public const string StorytellerCalmId = "StorytellerCalm";
    public const string StorytellerInsaneId = "StorytellerInsane";

    private static readonly ProtoId<GamePresetPoolPrototype> StorytellerPoolPrototypeId = "StorytellerPresetPool";

    /// <summary>
    /// Gets Storyteller presets that are currently available for a vote.
    /// </summary>
    /// <param name="excludedPresets">
    /// Preset IDs to omit from the result. Each item is a <see cref="GamePresetPrototype.ID"/> that is temporarily unavailable,
    /// for example because it was selected recently by rotation. When <see langword="null"/>, the current
    /// <see cref="GameTicker.ExcludedPresets"/> collection is used.
    /// </param>
    /// <returns>
    /// A dictionary whose key is an eligible Storyteller <see cref="GamePresetPrototype.ID"/> and whose value is its
    /// <see cref="GamePresetPrototype.ModeTitle"/> localization ID, not localized text.
    /// </returns>
    /// <example>
    /// <code>
    /// var excluded = new HashSet&lt;string&gt; { "StorytellerInsane" };
    /// var options = storyteller.GetAvailableVotePresets(excluded);
    /// </code>
    /// </example>
    public Dictionary<string, string> GetAvailableVotePresets(IReadOnlySet<string>? excludedPresets = null)
    {
        var storytellerPool = new Dictionary<string, MinMax>();
        AdjustPresetPool(storytellerPool);

        excludedPresets ??= GameTicker.ExcludedPresets.ToHashSet();
        return GameTicker.GetEligibleVotePresets(storytellerPool, _playerManager.PlayerCount, excludedPresets);
    }

    /// <summary>
    /// Adds the configured Storyteller presets to a game preset pool and applies Storyteller rotation rules.
    /// </summary>
    /// <param name="presets">
    /// The mutable pool to update in place. Each key is a <see cref="GamePresetPrototype.ID"/> and each value contains player limits:
    /// Its <see cref="MinMax.Min"/> field is the inclusive minimum player count and its <see cref="MinMax.Max"/> field is the inclusive maximum player count.
    /// Existing entries with the same ID are retained unless the Storyteller override setting clears the entire dictionary first.
    /// </param>
    /// <remarks>
    /// When Storyteller is disabled, this method leaves <paramref name="presets"/> unchanged. When the override setting is enabled,
    /// it clears the pool before adding Storyteller entries.
    /// </remarks>
    /// <example>
    /// <code>
    /// var presets = new Dictionary&lt;string, MinMax&gt;(presetPool.Presets);
    /// storyteller.AdjustPresetPool(presets);
    /// </code>
    /// </example>
    public void AdjustPresetPool(Dictionary<string, MinMax> presets)
    {
        if (!_cfg.GetCVar(SunriseCCVars.StorytellerEnabled))
            return;

        if (!ProtoMan.TryIndex(StorytellerPoolPrototypeId, out var poolPrototype))
        {
            Log.Error($"Storyteller preset pool '{StorytellerPoolPrototypeId}' not found!");
            return;
        }

        if (_cfg.GetCVar(SunriseCCVars.StorytellerOverridePresetPool))
            presets.Clear();

        foreach (var (presetId, limits) in poolPrototype.Presets)
        {
            presets.TryAdd(presetId, limits);
        }

        if (_cfg.GetCVar(SunriseCCVars.StorytellerRotationEnabled))
            ApplyRotationFilter(presets, _cfg.GetCVar(SunriseCCVars.StorytellerRotationCounter));
    }

    public static bool IsStorytellerPreset(string presetId)
    {
        return presetId is StorytellerClassicId or StorytellerCalmId or StorytellerInsaneId;
    }

    public static string GetVoteOptionLocId(string presetId)
    {
        return presetId switch
        {
            StorytellerCalmId => "ui-vote-storyteller-type-calm",
            StorytellerClassicId => "ui-vote-storyteller-type-classic",
            StorytellerInsaneId => "ui-vote-storyteller-type-insane",
            _ => GetNameLocId(presetId),
        };
    }

    public static string GetNameLocId(string presetId)
    {
        return presetId switch
        {
            StorytellerCalmId => "ui-vote-storyteller-type-calm-name",
            StorytellerClassicId => "ui-vote-storyteller-type-classic-name",
            StorytellerInsaneId => "ui-vote-storyteller-type-insane-name",
            _ => presetId,
        };
    }

    /// <summary>
    /// Removes Storyteller presets from a mutable pool according to the current rotation state.
    /// </summary>
    /// <param name="presets">
    /// The pool modified in place. Each key is a preset ID; each value stores the inclusive minimum and maximum player counts
    /// in <see cref="MinMax.Min"/> and <see cref="MinMax.Max"/>, which are used to determine whether another preset can replace Calm.
    /// </param>
    /// <param name="state">The configured Storyteller rotation state.</param>
    private void ApplyRotationFilter(Dictionary<string, MinMax> presets, int state)
    {
        if (state == 1)
        {
            presets.Remove(StorytellerInsaneId);
            return;
        }

        if (state != 2 || !presets.ContainsKey(StorytellerCalmId))
            return;

        foreach (var (presetId, limits) in presets)
        {
            if (presetId == StorytellerCalmId)
                continue;

            if (!_cfg.GetCVar(SunriseCCVars.IgnorePresetPlayerLimits) &&
                !GameTicker.IsPlayerCountWithinLimits(limits, _playerManager.PlayerCount))
                continue;

            presets.Remove(StorytellerCalmId);
            return;
        }
    }
}
