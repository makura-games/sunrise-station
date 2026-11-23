using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Storyteller.Prototypes;

/// <summary>
/// Storyteller per-material strength weight override. ID matches <see cref="Materials.MaterialPrototype"/>.
/// </summary>
[Prototype]
public sealed partial class StorytellerMaterialWeightPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public float Weight = 1f;
}
