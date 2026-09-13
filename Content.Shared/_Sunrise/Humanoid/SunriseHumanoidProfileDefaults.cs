using Content.Shared._Sunrise;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Humanoid;

public static class SunriseHumanoidProfileDefaults
{
    public static readonly ProtoId<SpeciesPrototype> DefaultSpecies = "Human";

    public static ProtoId<BodyTypePrototype> DefaultBodyType = "HumanNormal";

    public const string DefaultVoice = "Voljin";

    public static readonly Dictionary<Sex, string> DefaultSexVoice = new()
    {
        { Sex.Male, "Voljin" },
        { Sex.Female, "Amina" },
        { Sex.Unsexed, "Charlotte" },
    };

    public static ProtoId<BodyTypePrototype> GetDefaultBodyType(
        SpeciesPrototype? species,
        Sex sex,
        IPrototypeManager prototype)
    {
        if (species is null)
            return DefaultBodyType;

        foreach (var bodyTypeId in species.BodyTypes)
        {
            if (prototype.TryIndex<BodyTypePrototype>(bodyTypeId, out var bodyType) &&
                !bodyType.SexRestrictions.Contains(sex))
            {
                return bodyTypeId;
            }
        }

        return DefaultBodyType;
    }

    public static bool IsBodyTypeAllowed(
        SpeciesPrototype species,
        ProtoId<BodyTypePrototype> bodyTypeId,
        Sex sex,
        IPrototypeManager prototype)
    {
        return species.BodyTypes.Contains(bodyTypeId) &&
               prototype.TryIndex<BodyTypePrototype>(bodyTypeId, out var bodyType) &&
               !bodyType.SexRestrictions.Contains(sex);
    }
}
