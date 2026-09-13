using System.Numerics;
using Content.Shared._Sunrise.MarkingEffects;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Humanoid;

/// <summary>
/// Holds all of the data for importing / exporting character profiles.
/// </summary>
[DataDefinition]
public sealed partial class HumanoidProfileExportV1
{
    [DataField]
    public string ForkId;

    [DataField]
    public int Version = 1;

    [DataField(required: true)]
    public HumanoidCharacterProfileV1 Profile = default!;

    public HumanoidProfileExportV2 ToV2()
    {
        return new()
        {
            ForkId = ForkId,
            Version = 2,
            Profile = Profile.ToV2()
        };
    }
}

[DataDefinition, Serializable]
public sealed partial class HumanoidCharacterProfileV1
{
    [DataField("_jobPriorities")]
    public Dictionary<ProtoId<JobPrototype>, JobPriority> JobPriorities = new();

    [DataField("_antagPreferences")]
    public HashSet<ProtoId<AntagPrototype>> AntagPreferences = new();

    [DataField("_traitPreferences")]
    public HashSet<ProtoId<TraitPrototype>> TraitPreferences = new();

    [DataField("_loadouts")]
    public Dictionary<string, RoleLoadout> Loadouts = new();

    [DataField]
    public string Name;

    [DataField]
    public string FlavorText;

    [DataField]
    public ProtoId<SpeciesPrototype> Species;

    [DataField]
    public int Age;

    [DataField]
    public Sex Sex;

    [DataField]
    public Gender Gender;

    [DataField]
    public HumanoidCharacterAppearanceV1 Appearance;

    [DataField]
    public SpawnPriorityPreference SpawnPriority;

    [DataField]
    public PreferenceUnavailableMode PreferenceUnavailable;

    public HumanoidCharacterProfile ToV2()
    {
        return new(Name, FlavorText, Species, Age, Sex, Gender, Appearance.ToV2(Species), SpawnPriority, JobPriorities, PreferenceUnavailable, AntagPreferences, TraitPreferences, Loadouts);
    }
}


[DataDefinition, Serializable]
public sealed partial class HumanoidCharacterAppearanceV1
{
    [DataField("hair")]
    public string HairStyleId;

    [DataField]
    public Color HairColor;

    // Sunrise edit start - legacy hair effect import
    [DataField]
    public MarkingEffect? HairMarkingEffect;

    [DataField]
    public MarkingEffectType HairMarkingEffectType = MarkingEffectType.Color;
    // Sunrise edit end

    [DataField("facialHair")]
    public string FacialHairStyleId;

    [DataField]
    public Color FacialHairColor;

    // Sunrise edit start - legacy facial hair effect import
    [DataField]
    public MarkingEffect? FacialHairMarkingEffect;

    [DataField]
    public MarkingEffectType FacialHairMarkingEffectType = MarkingEffectType.Color;
    // Sunrise edit end

    [DataField]
    public Color EyeColor;

    [DataField]
    public Color SkinColor;

    [DataField]
    public List<Marking> Markings = new();

    public HumanoidCharacterAppearance ToV2(ProtoId<SpeciesPrototype> species)
    {
        var markingManager = IoCManager.Resolve<MarkingManager>();

        var incomingMarkings = Markings.ShallowClone();
        // Sunrise edit start - convert legacy hairs to new one
        if (HairStyleId != string.Empty)
            incomingMarkings.Add(new(
                HairStyleId,
                new List<Color> { HairColor },
                new List<MarkingEffect> { CreateLegacyHairEffect(HairMarkingEffect, HairMarkingEffectType, HairColor) }));
        if (FacialHairStyleId != string.Empty)
            incomingMarkings.Add(new(
                FacialHairStyleId,
                new List<Color> { FacialHairColor },
                new List<MarkingEffect> { CreateLegacyHairEffect(FacialHairMarkingEffect, FacialHairMarkingEffectType, FacialHairColor) }));
        // Sunrise edit end

        return new HumanoidCharacterAppearance(EyeColor, SkinColor, markingManager.ConvertMarkings(incomingMarkings, species));
    }

    // Sunrise edit start - legacy hair effect compability
    private static MarkingEffect CreateLegacyHairEffect(MarkingEffect? effect, MarkingEffectType type, Color baseColor)
    {
        return effect is not null
            ? MarkingEffectCompatibility.WithLegacyBaseColor(effect, baseColor)
            : MarkingEffectCompatibility.CreateFromType(type, baseColor);
    }
    // Sunrise edit end
}
