using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Сущность появляется на свет уже заражённой. Болезнь выбирается из списка случайно.
/// </summary>
/// <remarks>
/// Так работают и черта «лёгкая простуда», и заражённое зверьё из вентиляции:
/// в обоих случаях болезнь нужно завести в раунд вместе с носителем, а не через событие.
/// Компонент снимается сразу после заражения.
/// </remarks>
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
