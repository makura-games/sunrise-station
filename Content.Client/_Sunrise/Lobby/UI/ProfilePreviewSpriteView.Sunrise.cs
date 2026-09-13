using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client._Sunrise.Humanoid;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Clothing;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Sunrise.Interfaces.Shared;
using Robust.Shared.IoC;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Lobby.UI.ProfileEditorControls;

public sealed partial class ProfilePreviewSpriteView
{
    private ISharedSponsorsManager? _sponsorsManager;

    private void InitializeSunriseProfilePreview()
    {
        IoCManager.Instance!.TryResolveType(out _sponsorsManager);
    }

    private void ApplySunriseProfileToPreview(HumanoidCharacterProfile profile)
    {
        EntMan.System<HumanoidProfileSystem>().ApplyProfileTo(PreviewDummy, profile);
        EntMan.System<SunriseHumanoidProfileSystem>().ApplyProfileTo(PreviewDummy, profile);
        EntMan.System<SunriseHumanoidProfileVisualSystem>().Refresh(PreviewDummy);
    }

    private bool TryGetSunrisePreviewLoadout(
        HumanoidCharacterProfile profile,
        JobPrototype job,
        [NotNullWhen(true)] out RoleLoadout? loadout)
    {
        var jobLoadoutId = LoadoutSystem.GetJobPrototype(job.ID);
        var effectiveJobLoadoutId = LoadoutSystem.GetEffectiveRolePrototype(jobLoadoutId, _prototypeManager);
        if (!_prototypeManager.HasIndex<RoleLoadoutPrototype>(effectiveJobLoadoutId))
        {
            loadout = null;
            return false;
        }

        var sponsorPrototypes = _sponsorsManager?.GetClientPrototypes().ToArray() ?? [];
        loadout = profile.GetLoadoutOrDefault(
            jobLoadoutId,
            _playerManager.LocalSession,
            profile.Species,
            EntMan,
            _prototypeManager,
            sponsorPrototypes);
        return true;
    }
}
