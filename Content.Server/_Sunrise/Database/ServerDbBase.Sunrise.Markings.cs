using Content.Shared._Sunrise.MarkingEffects;
using Content.Shared.Humanoid.Markings;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    private static void StoreSunriseHairEffects(Profile profile, Marking? hair, Marking? facialHair)
    {
        var hairEffect = hair?.MarkingEffects is { Count: > 0 } hairEffects
            ? hairEffects[0]
            : null;
        var facialHairEffect = facialHair?.MarkingEffects is { Count: > 0 } facialHairEffects
            ? facialHairEffects[0]
            : null;

        profile.HairColorType = (int)(hairEffect?.Type ?? MarkingEffectType.Color);
        profile.HairExtendedColor = hairEffect?.ToString() ?? string.Empty;
        profile.FacialHairColorType = (int)(facialHairEffect?.Type ?? MarkingEffectType.Color);
        profile.FacialHairExtendedColor = facialHairEffect?.ToString() ?? string.Empty;
    }
}
