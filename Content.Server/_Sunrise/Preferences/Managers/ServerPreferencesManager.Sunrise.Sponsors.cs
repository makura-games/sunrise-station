using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Preferences.Managers;

public sealed partial class ServerPreferencesManager
{
    private ISharedSponsorsManager? _sponsors;

    private void InitializeSunriseSponsors()
    {
        IoCManager.Instance!.TryResolveType(out _sponsors);
    }

    private string[] GetSunriseSponsorPrototypes(NetUserId userId)
    {
        return _sponsors != null && _sponsors.TryGetPrototypes(userId, out var prototypes)
            ? prototypes.ToArray()
            : [];
    }

    private void ValidateSunriseProfile(HumanoidCharacterProfile profile, ICommonSession session)
    {
        profile.EnsureValid(session, _dependencies, GetSunriseSponsorPrototypes(session.UserId));
    }

    private int GetMaxUserCharacterSlots(NetUserId userId)
    {
        var maxSlots = _cfg.GetCVar(CCVars.GameMaxCharacterSlots);
        var extraSlots = _sponsors?.GetExtraCharSlots(userId) ?? 0;
        return maxSlots + extraSlots;
    }

    private static void EnsureSunriseSelectedCharacterIndex(PlayerPreferences preferences, int maxSlots)
    {
        var selected = preferences.SelectedCharacterIndex;
        if (selected >= 0 &&
            selected < maxSlots &&
            preferences.Characters.ContainsKey(selected))
        {
            return;
        }

        foreach (var index in preferences.Characters.Keys)
        {
            if (index < 0 || index >= maxSlots)
                continue;

            preferences.SelectedCharacterIndex = index;
            return;
        }

        preferences.SelectedCharacterIndex = preferences.Characters.FirstOrDefault().Key;
    }
}
