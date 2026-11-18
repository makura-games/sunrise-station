// ReSharper disable once CheckNamespace
namespace Content.Shared.Research.Prototypes;

public sealed partial class TechnologyPrototype
{
    /// <summary>
    /// Whether this technology can be purchased repeatedly.
    /// </summary>
    [DataField]
    public bool Repeatable;

    /// <summary>
    /// Whether this technology requires completion of the regular research tree.
    /// </summary>
    [DataField]
    public bool RequiresResearchCompletion;
}
