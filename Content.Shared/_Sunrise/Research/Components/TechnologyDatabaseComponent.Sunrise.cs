using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Research.Components;

public sealed partial class TechnologyDatabaseComponent
{
    /// <summary>
    /// Additional disciplines selected after expanding the research database.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<TechDisciplinePrototype>> AdditionalDisciplines = [];

    /// <summary>
    /// Maximum number of disciplines available beyond the lockout tier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ResearchDisciplineLimit = 1;

    /// <summary>
    /// Maximum tier for whole technologies researched in this database.
    /// Individually imported recipes are not subject to this limit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? MaximumTechnologyTier;

    /// <summary>
    /// Overrides the tier at which this database locks in a discipline.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? DisciplineLockoutTier;
}
