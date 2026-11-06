// Suppress namespace warning
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Research.Prototypes;

public sealed partial class TechDisciplinePrototype
{
    /// <summary>
    /// Multiplier determining how much this discipline's technologies contribute to the crew's strength metric.
    /// Used by the StorytellerSystem to evaluate crew power.
    /// </summary>
    [DataField("storytellerUsefulness")]
    public float StorytellerUsefulness = 1.0f;
}
