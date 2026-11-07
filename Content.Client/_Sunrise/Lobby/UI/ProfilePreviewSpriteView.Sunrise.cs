using System.Diagnostics.CodeAnalysis;
using Content.Client._Sunrise.Humanoid;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Clothing;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Client.Lobby.UI.ProfileEditorControls;

public sealed partial class ProfilePreviewSpriteView
{
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

        loadout = profile.GetLoadoutOrDefault(
            jobLoadoutId,
            _playerManager.LocalSession,
            _prototypeManager);
        return true;
    }
}
