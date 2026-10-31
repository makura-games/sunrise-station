using Content.Server.Humanoid.Components;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Humanoid.Systems;

public sealed partial class RandomHumanoidAppearanceSystem
{
    [Dependency] private SunriseHumanoidBodySystem _sunriseBody = default!;
    [Dependency] private SunriseHumanoidMarkingSystem _sunriseMarking = default!;
    [Dependency] private SunriseHumanoidProfileSystem _sunriseProfile = default!;

    public void ApplySunriseProfileTo(EntityUid uid, HumanoidCharacterProfile profile)
    {
        _sunriseProfile.ApplyProfileTo(uid, profile);
    }

    public void ApplySunriseAppearanceOverrides(EntityUid uid, RandomHumanoidAppearanceComponent component)
    {
        if (component.SkinColor != null)
            _sunriseBody.SetSkinColor(uid, component.SkinColor.Value);

        if (component.Hair != null)
            _sunriseMarking.SetMarkingId(uid, HumanoidVisualLayers.Hair, 0, component.Hair);

        if (component.HairColor != null)
            _sunriseMarking.SetAllMarkingColors(uid, HumanoidVisualLayers.Hair, component.HairColor.Value);

        if (component.FacialHairColor != null)
            _sunriseMarking.SetAllMarkingColors(uid, HumanoidVisualLayers.FacialHair, component.FacialHairColor.Value);
    }
}
