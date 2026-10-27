using Content.Server.GameTicking.Presets;
using Content.Shared.Destructible.Thresholds;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.GameTicking.Components;

namespace Content.Server.GameTicking;

/// <summary>
/// Provides preset eligibility checks used by game mode selection and votes.
/// </summary>
public sealed partial class GameTicker
{
    /// <summary>
    /// Determines whether a game preset can be selected for the specified player count.
    /// </summary>
    /// <param name="preset">The preset to validate, or <see langword="null"/> when no preset is available.</param>
    /// <param name="playerCount">The current number of players used to validate preset and game rule limits.</param>
    /// <returns><see langword="true"/> when the preset and all of its game rules support <paramref name="playerCount"/>; otherwise, <see langword="false"/>.</returns>
    public bool IsPresetEligible(GamePresetPrototype? preset, int playerCount)
    {
        return IsPresetEligible(preset, playerCount, Factory.GetComponentName<GameRuleComponent>());
    }

    public bool IsPresetEligible(GamePresetPrototype? preset, int playerCount, string ruleComponentName)
    {
        return IsPresetEligible(preset, playerCount, ruleComponentName, false);
    }

    private bool IsPresetEligible(
        GamePresetPrototype? preset,
        int playerCount,
        string ruleComponentName,
        bool ignorePlayerLimits)
    {
        if (preset == null)
            return false;

        if (!ignorePlayerLimits && playerCount < preset.MinPlayers)
            return false;

        if (!ignorePlayerLimits && playerCount > preset.MaxPlayers)
            return false;

        foreach (var ruleId in preset.Rules)
        {
            if (!_prototypeManager.TryIndex(ruleId, out var rule) ||
                !rule.TryGetComponent<GameRuleComponent>(ruleComponentName, out var ruleComponent))
            {
                _sawmill.Error($"Encountered invalid rule {ruleId} in preset {preset.ID}");
                return false;
            }

            if (!ignorePlayerLimits && ruleComponent.MinPlayers > playerCount && ruleComponent.CancelPresetOnTooFewPlayers)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Builds the game preset options that are currently valid for a vote.
    /// </summary>
    /// <param name="pool">
    /// A preset pool where each key is a <see cref="GamePresetPrototype.ID"/> and each value contains its player limits.
    /// The value's <see cref="MinMax.Min"/> field is the inclusive minimum player count, and its
    /// <see cref="MinMax.Max"/> field is the inclusive maximum player count.
    /// </param>
    /// <param name="playerCount">The current player count checked against the limits stored in <paramref name="pool"/> and in each preset.</param>
    /// <param name="excludedPresets">
    /// Preset IDs that must not be returned. Each set item is a <see cref="GamePresetPrototype.ID"/>, usually an ID temporarily excluded by rotation.
    /// Pass <see langword="null"/> when no presets need to be excluded.
    /// </param>
    /// <returns>
    /// A dictionary whose key is an eligible <see cref="GamePresetPrototype.ID"/> and whose value is that preset's
    /// <see cref="GamePresetPrototype.ModeTitle"/> localization ID, not localized text.
    /// </returns>
    /// <remarks>
    /// When <see cref="SunriseCCVars.IgnorePresetPlayerLimits"/> is enabled, player limits from the pool, preset, and game rules
    /// are ignored for vote options. Prototype validity and <see cref="GamePresetPrototype.ShowInVote"/> are still checked.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = ticker.GetEligibleVotePresets(
    ///     new Dictionary&lt;string, MinMax&gt;
    ///     {
    ///         ["Traitor"] = new(5, 200),
    ///         ["Nukeops"] = new(30, 200),
    ///     },
    ///     playerCount,
    ///     excludedPresets);
    /// </code>
    /// </example>
    public Dictionary<string, string> GetEligibleVotePresets(
        IReadOnlyDictionary<string, MinMax> pool,
        int playerCount,
        IReadOnlySet<string>? excludedPresets = null)
    {
        var result = new Dictionary<string, string>();
        var ignorePlayerLimits = _cfg.GetCVar(SunriseCCVars.IgnorePresetPlayerLimits);
        var ruleComponentName = Factory.GetComponentName<GameRuleComponent>();

        foreach (var (presetId, limits) in pool)
        {
            if (excludedPresets?.Contains(presetId) == true)
                continue;

            if (!ignorePlayerLimits && !IsPlayerCountWithinLimits(limits, playerCount))
                continue;

            if (!_prototypeManager.TryIndex<GamePresetPrototype>(presetId, out var preset))
                continue;

            if (!preset.ShowInVote)
                continue;

            if (!IsPresetEligible(preset, playerCount, ruleComponentName, ignorePlayerLimits))
                continue;

            result[preset.ID] = preset.ModeTitle;
        }

        return result;
    }

    /// <summary>
    /// Determines whether a player count is within the limits stored in a preset pool entry.
    /// </summary>
    /// <param name="limits">
    /// The <see cref="MinMax.Min"/> field is the inclusive minimum and the <see cref="MinMax.Max"/> field is the inclusive maximum.
    /// </param>
    /// <param name="playerCount">The player count to test.</param>
    /// <returns><see langword="true"/> when <paramref name="playerCount"/> is within the specified limits; otherwise, <see langword="false"/>.</returns>
    /// <example>
    /// <code>
    /// var canStart = ticker.IsPlayerCountWithinLimits(new MinMax(20, 200), playerCount);
    /// </code>
    /// </example>
    public bool IsPlayerCountWithinLimits(MinMax limits, int playerCount)
    {
        return playerCount >= limits.Min && playerCount <= limits.Max;
    }
}
