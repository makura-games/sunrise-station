using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Сущность спавнится уже заражённой. Болезнь выбирается из списка случайно.
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseStartInfectedComponent : Component
{
    /// <summary>
    /// Болезни, из которых выбирается одна.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Diseases = new();

    /// <summary>
    /// Шанс, что носитель действительно окажется заражён.
    /// </summary>
    [DataField]
    public float Chance = 1f;
}
