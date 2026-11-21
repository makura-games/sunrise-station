using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Предмет с малым шансом оказаться заражённым при появлении.
/// </summary>
/// <remarks>
/// Висит на сыром мясе: пара процентов на то, что плохо прожаренный стейк окажется с личинками.
/// Выпал шанс — предмет получает <see cref="DiseaseSourceComponent"/> и становится очагом,
/// не выпал — компонент просто снимается, и предмет остаётся обычной едой.
/// </remarks>
[RegisterComponent]
public sealed partial class DiseaseContaminationComponent : Component
{
    /// <summary>
    /// Болезни, из которых выбирается одна.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Diseases = new();

    /// <summary>
    /// Шанс заражённости.
    /// </summary>
    [DataField]
    public float Chance = 0.02f;

    /// <summary>
    /// Какими путями предмет заражает.
    /// </summary>
    [DataField]
    public DiseaseTransmissionRoute Routes = DiseaseTransmissionRoute.Ingestion;

    /// <summary>
    /// Шанс заражения при съедании.
    /// </summary>
    [DataField]
    public float IngestionChance = 1f;

    /// <summary>
    /// Шанс заражения при контакте.
    /// </summary>
    [DataField]
    public float ContactChance;
}
