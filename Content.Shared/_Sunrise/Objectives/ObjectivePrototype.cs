using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Objectives;

/// <summary>
/// Defines a reusable objective with optional presentation metadata.
/// </summary>
[Prototype]
public sealed partial class ObjectivePrototype : IPrototype
{
    /// <summary>
    /// Stable prototype identifier.
    /// </summary>
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Optional localized title for consumers that present the objective to a player.
    /// </summary>
    [DataField]
    public LocId? Name;

    /// <summary>
    /// Optional localized description for consumers that present the objective to a player.
    /// </summary>
    [DataField]
    public LocId? Description;

    /// <summary>
    /// Objective behavior.
    /// </summary>
    [DataField(required: true)]
    public ObjectiveDefinition Definition = new();
}
