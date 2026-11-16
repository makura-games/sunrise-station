using Content.Shared._Sunrise.Helpers;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.StationRecords;

/// <summary>
///     General station record. Indicates the crewmember's name and job.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial record GeneralStationRecord : StationRecord
{
    /// <summary>
    ///     Name tied to this station record.
    /// </summary>
    [DataField]
    public string Name = string.Empty;

    /// <summary>
    ///     Age of the person that this station record represents.
    /// </summary>
    [DataField]
    public int Age;

    /// <summary>
    ///     Job title tied to this station record.
    /// </summary>
    [DataField]
    public string JobTitle = string.Empty;

    /// <summary>
    ///     Job icon tied to this station record.
    /// </summary>
    [DataField]
    public string JobIcon = string.Empty;

    [DataField]
    public string JobPrototype = string.Empty;

    /// <summary>
    ///     Species tied to this station record.
    /// </summary>
    [DataField]
    public string Species = string.Empty;

    /// <summary>
    ///     Gender identity tied to this station record.
    /// </summary>
    /// <remarks>Sex should be placed in a medical record, not a general record.</remarks>
    [DataField]
    public Gender Gender = Gender.Epicene;

    /// <summary>
    ///     The priority to display this record at, taken from the station's job-weight profile.
    /// </summary>
    [DataField]
    public int DisplayPriority;

    /// <summary>
    ///     Fingerprint of the person.
    /// </summary>
    [DataField]
    public string? Fingerprint;

    /// <summary>
    ///     DNA of the person.
    /// </summary>
    [DataField]
    public string? DNA;

    // Sunrise added start
    [DataField]
    public bool Silicon;

    [DataField]
    public HumanoidCharacterProfile? HumanoidProfile;

    [DataField]
    public string Personality = string.Empty;

    [NonSerialized] private const int MaxNameLength = 64;
    [NonSerialized] private const int MaxAge = 10000;
    [NonSerialized] private const int MaxFingerprintLength = 32;
    [NonSerialized] private const int MaxDnaLength = 16;
    [NonSerialized] private const int MaxPersonalityLength = 1024;

    [NonSerialized] private static readonly ProtoId<JobPrototype> FallbackJobPrototype = "Passenger";
    [NonSerialized] private static readonly ProtoId<SpeciesPrototype> FallbackSpeciesPrototype = "Human";

    /// <summary>
    /// Санитизирует данные, требуется для механики изменения и сохранения данных в консоли станционного учета.
    /// </summary>
    public static GeneralStationRecord SanitizeRecord(GeneralStationRecord original, in IPrototypeManager prototype)
    {
        var updated = original with
        {
            Name = original.Name.SanitizeInput(MaxNameLength),
            Age = original.Age <= MaxAge ? original.Age : MaxAge,
            Species = prototype.TryIndex<SpeciesPrototype>(original.Species, out var species) ? species.ID : FallbackSpeciesPrototype,
            JobPrototype = prototype.TryIndex<JobPrototype>(original.JobPrototype, out var job) ? job.ID : FallbackJobPrototype,
            Fingerprint = original.Fingerprint.SanitizeInput(MaxFingerprintLength),
            DNA = original.DNA.SanitizeInput(MaxDnaLength),
            Personality = original.Personality.SanitizeInput(MaxPersonalityLength),
        };

        return updated;
    }

    // Sunrise added end
}
