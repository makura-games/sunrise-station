using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Smell.Prototypes;

/// <summary>
/// Personal scent profile of a species/creature: note pools from which the generator
/// picks one note per pool, seeded by the character's traits.
/// </summary>
[Prototype]
public sealed partial class PersonalScentProfilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Note pools: exactly one note is taken from each.
    /// </summary>
    [DataField(required: true)]
    public List<ScentNotePool> NotePools { get; private set; } = [];
}

/// <summary>
/// Pool of interchangeable notes of a single scent layer (base/nature/accent).
/// </summary>
[DataDefinition]
public sealed partial class ScentNotePool
{
    /// <summary>
    /// The layer's note variants.
    /// </summary>
    [DataField(required: true)]
    public List<LocId> Notes { get; private set; } = [];
}
