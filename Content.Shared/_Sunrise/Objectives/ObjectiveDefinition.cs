namespace Content.Shared._Sunrise.Objectives;

/// <summary>
/// Combines behavior-only conditions using all-of and any-of gates.
/// </summary>
[DataDefinition]
public sealed partial class ObjectiveDefinition
{
    /// <summary>
    /// Conditions that must all be satisfied.
    /// </summary>
    [DataField]
    public List<ObjectiveCondition> All = [];

    /// <summary>
    /// Alternative conditions. When non-empty, at least one must be satisfied.
    /// </summary>
    [DataField]
    public List<ObjectiveCondition> Any = [];
}
