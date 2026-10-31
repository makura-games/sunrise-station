using System.IO;
using System.Linq;
using Content.Client.Lobby.UI.Loadouts;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Clothing;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private IEnumerable<SpeciesPrototype> GetSunriseAvailableSpecies()
    {
        var sponsorPrototypes = _sponsorsMgr?.GetClientPrototypes() ?? [];

        return _prototypeManager.EnumeratePrototypes<SpeciesPrototype>()
            .Where(species => species.RoundStart &&
                (!species.SponsorOnly || sponsorPrototypes.Contains(species.ID)));
    }

    private FlavorText.FlavorText CreateSunriseFlavorText()
    {
        return new FlavorText.FlavorText(
            _sponsorsMgr,
            _cfgManager.GetCVar(SunriseCCVars.FlavorTextSponsorOnly),
            _cfgManager.GetCVar(SunriseCCVars.FlavorTextBaseLength));
    }

    private HumanoidCharacterProfile DeserializeSunriseProfile(Stream stream)
    {
        return HumanoidCharacterProfile.FromStream(
            stream,
            _playerManager.LocalSession!,
            GetSunriseSponsorPrototypes());
    }

    private LoadoutWindow CreateSunriseLoadoutWindow(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        RoleLoadoutPrototype prototype,
        ICommonSession session,
        IDependencyCollection collection)
    {
        return new LoadoutWindow(profile, loadout, prototype, session, collection, _sponsorsMgr);
    }

    private void SetSunriseDefaultLoadout(RoleLoadout loadout)
    {
        loadout.SetDefault(
            Profile,
            _playerManager.LocalSession,
            _prototypeManager,
            GetSunriseSponsorPrototypes());
    }

    private ProtoId<RoleLoadoutPrototype> GetSunriseEffectiveRoleLoadoutPrototype(string jobId)
    {
        return LoadoutSystem.GetEffectiveJobPrototype(jobId, _prototypeManager);
    }

    private string[] GetSunriseSponsorPrototypes()
    {
        return _sponsorsMgr?.GetClientPrototypes().ToArray() ?? [];
    }
}
