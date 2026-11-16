using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Research.Events;

/// <summary>
/// Raised when a research discipline is selected.
/// </summary>
[ByRefEvent]
public readonly record struct ResearchDisciplineSelectedEvent(ProtoId<TechDisciplinePrototype> Discipline);

/// <summary>
/// Increases the number of available research disciplines.
/// </summary>
[DataDefinition]
public sealed partial class ResearchDisciplineUnlockEvent : EntityEventArgs
{
    [DataField]
    public int Amount;
}

/// <summary>
/// Transfers funds to the science department account.
/// </summary>
[DataDefinition]
public sealed partial class ResearchCargoGrantEvent : EntityEventArgs
{
    [DataField]
    public int Amount;
}
