using Content.Shared.Silicons.Laws;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Objectives.Components;

[RegisterComponent]
public sealed partial class EnsureLawBoundEntitiesHaveNoLawsConditionComponent : Component
{
    /// <summary>
    /// Number of law-bound entities that must have no laws for success.
    /// </summary>
    [DataField]
    public int EntitiesToFree = 3;

    /// <summary>
    /// Lawsets that count as freeing a law-bound entity even if they still contain an informational law.
    /// </summary>
    [DataField]
    public List<ProtoId<SiliconLawsetPrototype>> FreedLawsets = ["FreeLawset"];

    /// <summary>
    /// Optional whitelist of entities that can count toward progress.
    /// </summary>
    [DataField]
    public EntityWhitelist? LawEntityWhitelist;

    /// <summary>
    /// Optional blacklist of entities that should never count toward progress.
    /// </summary>
    [DataField]
    public EntityWhitelist? LawEntityBlacklist;
}
