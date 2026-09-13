using System.Linq;
using Content.Shared._Sunrise;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Preferences;

/// <summary>
/// Sunrise-only character profile data kept out of the upstream humanoid appearance model.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class SunriseCharacterProfile : IEquatable<SunriseCharacterProfile>
{
    [DataField]
    public ProtoId<TTSVoicePrototype> Voice = SunriseHumanoidProfileDefaults.DefaultVoice;

    [DataField]
    public ProtoId<BodyTypePrototype> BodyType = SunriseHumanoidProfileDefaults.DefaultBodyType;

    [DataField]
    public float Width = 1f;

    [DataField]
    public float Height = 1f;

    [DataField]
    private Dictionary<ProtoId<JobPrototype>, LocId> _jobAlternativeTitles = new();

    public IReadOnlyDictionary<ProtoId<JobPrototype>, LocId> JobAlternativeTitles => _jobAlternativeTitles;

    public SunriseCharacterProfile()
    {
    }

    public SunriseCharacterProfile(SunriseCharacterProfile other)
    {
        Voice = other.Voice;
        BodyType = other.BodyType;
        Width = other.Width;
        Height = other.Height;
        _jobAlternativeTitles = new(other._jobAlternativeTitles);
    }

    public static SunriseCharacterProfile DefaultForSpecies(ProtoId<SpeciesPrototype> species, Sex sex)
    {
        var prototype = IoCManager.Resolve<IPrototypeManager>();
        prototype.TryIndex(species, out var speciesPrototype);

        return DefaultForSpecies(speciesPrototype, sex);
    }

    public static SunriseCharacterProfile DefaultForSpecies(SpeciesPrototype? species, Sex sex)
    {
        var prototype = IoCManager.Resolve<IPrototypeManager>();
        var bodyType = SunriseHumanoidProfileDefaults.GetDefaultBodyType(species, sex, prototype);
        return new SunriseCharacterProfile
        {
            Voice = GetDefaultVoice(sex),
            BodyType = bodyType,
            Width = species?.DefaultWidth ?? 1f,
            Height = species?.DefaultHeight ?? 1f,
        };
    }

    public static SunriseCharacterProfile RandomForSpecies(SpeciesPrototype? species, Sex sex, IRobustRandom random, IPrototypeManager prototype)
    {
        var profile = DefaultForSpecies(species, sex);

        if (species is not null && species.BodyTypes.Count > 0)
        {
            var bodyTypes = species.BodyTypes
                .Where(id => SunriseHumanoidProfileDefaults.IsBodyTypeAllowed(species, id, sex, prototype))
                .ToArray();

            if (bodyTypes.Length > 0)
                profile.BodyType = random.Pick(bodyTypes);
        }

        var voices = prototype.EnumeratePrototypes<TTSVoicePrototype>()
            .Where(voice => HumanoidCharacterProfile.CanHaveVoice(voice, sex) && !voice.SponsorOnly)
            .ToArray();

        if (voices.Length > 0)
            profile.Voice = random.Pick(voices).ID;

        return profile;
    }

    public SunriseCharacterProfile WithVoice(ProtoId<TTSVoicePrototype> voice)
    {
        return new(this) { Voice = voice };
    }

    public SunriseCharacterProfile WithBodyType(ProtoId<BodyTypePrototype> bodyType)
    {
        return new(this) { BodyType = bodyType };
    }

    public SunriseCharacterProfile WithWidth(float width)
    {
        return new(this) { Width = width };
    }

    public SunriseCharacterProfile WithHeight(float height)
    {
        return new(this) { Height = height };
    }

    public SunriseCharacterProfile WithSize(float width, float height)
    {
        return new(this)
        {
            Width = width,
            Height = height,
        };
    }

    public SunriseCharacterProfile WithJobAlternativeTitle(ProtoId<JobPrototype> jobId, LocId? alternativeTitle)
    {
        var dictionary = new Dictionary<ProtoId<JobPrototype>, LocId>(_jobAlternativeTitles);

        if (alternativeTitle is null || string.IsNullOrEmpty(alternativeTitle.Value.Id))
            dictionary.Remove(jobId);
        else
            dictionary[jobId] = alternativeTitle.Value;

        return new(this)
        {
            _jobAlternativeTitles = dictionary,
        };
    }

    public SunriseCharacterProfile WithJobAlternativeTitles(Dictionary<ProtoId<JobPrototype>, LocId> alternativeTitles)
    {
        return new(this)
        {
            _jobAlternativeTitles = new(alternativeTitles),
        };
    }

    public SunriseCharacterProfile Validated(
        HumanoidCharacterProfile profile,
        SpeciesPrototype species,
        Sex sex,
        ICommonSession session,
        IDependencyCollection collection,
        string[] sponsorPrototypes)
    {
        var prototype = collection.Resolve<IPrototypeManager>();
        var result = new SunriseCharacterProfile(this);

        var bodyTypeAllowed = SunriseHumanoidProfileDefaults.IsBodyTypeAllowed(
            species,
            result.BodyType,
            sex,
            prototype);

        result.BodyType = bodyTypeAllowed
            ? result.BodyType
            : SunriseHumanoidProfileDefaults.GetDefaultBodyType(species, sex, prototype);

        result.Width = Math.Clamp(result.Width, species.MinWidth, species.MaxWidth);
        result.Height = Math.Clamp(result.Height, species.MinHeight, species.MaxHeight);

        if (!prototype.TryIndex<TTSVoicePrototype>(result.Voice, out var voice) ||
            !HumanoidCharacterProfile.CanHaveVoice(voice, sex) ||
            voice.SponsorOnly && !sponsorPrototypes.Contains(voice.ID))
        {
            result.Voice = GetDefaultVoice(sex);
        }

        var validAlternativeTitles = new Dictionary<ProtoId<JobPrototype>, LocId>();
        foreach (var (jobId, alternativeTitle) in result._jobAlternativeTitles)
        {
            if (!prototype.TryIndex<JobPrototype>(jobId, out var job))
                continue;

            if (job.AlternativeTitles.Contains(alternativeTitle))
                validAlternativeTitles[jobId] = alternativeTitle;
        }

        result._jobAlternativeTitles = validAlternativeTitles;
        return result;
    }

    public static ProtoId<TTSVoicePrototype> GetDefaultVoice(Sex sex)
    {
        return SunriseHumanoidProfileDefaults.DefaultSexVoice.GetValueOrDefault(sex, SunriseHumanoidProfileDefaults.DefaultVoice);
    }

    public bool Equals(SunriseCharacterProfile? other)
    {
        if (other is null)
            return false;

        return Voice == other.Voice &&
               BodyType == other.BodyType &&
               MathF.Abs(Width - other.Width) < 0.0001f &&
               MathF.Abs(Height - other.Height) < 0.0001f &&
               _jobAlternativeTitles.SequenceEqual(other._jobAlternativeTitles);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is SunriseCharacterProfile other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Voice, BodyType, Width, Height, _jobAlternativeTitles);
    }
}
