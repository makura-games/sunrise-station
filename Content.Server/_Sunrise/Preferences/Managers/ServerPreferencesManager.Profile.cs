using System.Collections.Generic;
using System.Linq;
using Content.Server.Database;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared._Sunrise.MarkingEffects;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Preferences.Managers;

public sealed partial class ServerPreferencesManager
{
    private static HumanoidCharacterProfile ApplySunriseProfileData(
        HumanoidCharacterProfile humanoid,
        Profile profile,
        Sex sex)
    {
        var voice = profile.Voice;
        if (voice == string.Empty)
            voice = SunriseHumanoidProfileDefaults.DefaultSexVoice[sex];

        var jobAlternativeTitles = profile.JobAlternativeTitles.ToDictionary(
            job => new ProtoId<JobPrototype>(job.JobName),
            job => new LocId(job.Title));

        return humanoid
            .WithVoice(voice)
            .WithBodyType(profile.BodyType)
            .WithSize(profile.Width, profile.Height)
            .WithJobAlternativeTitles(jobAlternativeTitles);
    }

    private static void AddSunriseLegacyHairMarkings(List<Marking> markings, Profile profile)
    {
        if (CreateSunriseLegacyHairMarking(
                profile.FacialHairName,
                profile.FacialHairColor,
                profile.FacialHairColorType,
                profile.FacialHairExtendedColor) is { } facialHair)
        {
            markings.Add(facialHair);
        }

        if (CreateSunriseLegacyHairMarking(
                profile.HairName,
                profile.HairColor,
                profile.HairColorType,
                profile.HairExtendedColor) is { } hair)
        {
            markings.Add(hair);
        }
    }

    private static Marking? CreateSunriseLegacyHairMarking(
        string? markingId,
        string? colorHex,
        int effectType,
        string? serializedEffect)
    {
        if (string.IsNullOrWhiteSpace(markingId))
            return null;

        var color = ParseSunriseLegacyColor(colorHex, Color.Black);
        var effects = MarkingEffectCompatibility.TryReadLegacyEffect(effectType, serializedEffect, color, out var effect)
            ? new List<MarkingEffect> { effect }
            : null;

        return new Marking(markingId, new List<Color> { color }, effects);
    }

    private static void ApplySunriseLegacyHairEffects(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings,
        Profile profile)
    {
        TryApplySunriseLegacyHairEffect(
            markings,
            HumanoidVisualLayers.Hair,
            profile.HairColor,
            profile.HairColorType,
            profile.HairExtendedColor);

        TryApplySunriseLegacyHairEffect(
            markings,
            HumanoidVisualLayers.FacialHair,
            profile.FacialHairColor,
            profile.FacialHairColorType,
            profile.FacialHairExtendedColor);
    }

    private static void TryApplySunriseLegacyHairEffect(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings,
        HumanoidVisualLayers layer,
        string? colorHex,
        int effectType,
        string? serializedEffect)
    {
        var color = ParseSunriseLegacyColor(colorHex, Color.Black);
        if (!MarkingEffectCompatibility.TryReadLegacyEffect(effectType, serializedEffect, color, out var effect))
            return;

        foreach (var organMarkings in markings.Values)
        {
            if (!organMarkings.TryGetValue(layer, out var layerMarkings))
                continue;

            for (var i = 0; i < layerMarkings.Count; i++)
            {
                var marking = layerMarkings[i];
                marking.SetMarkingEffect(0, effect.Clone());
                layerMarkings[i] = marking;
            }
        }
    }

    private static Color ParseSunriseLegacyColor(string? colorHex, Color fallback)
    {
        return string.IsNullOrWhiteSpace(colorHex)
            ? fallback
            : Color.TryFromHex(colorHex) ?? fallback;
    }
}
