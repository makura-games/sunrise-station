using Content.Server._Sunrise.StationEvents.Events;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.StationEvents.Components;

/// <summary>
/// Подбрасывает на станцию заражённый предмет: ящик с биоотходами, труп в техах и прочее.
/// </summary>
/// <remarks>
/// В отличие от вспышки с нулевым пациентом, у этого события есть осязаемая причина:
/// очаг можно найти и уничтожить, и это отдельная задача помимо лечения.
/// </remarks>
[RegisterComponent, Access(typeof(DiseaseSourceSpawnRule))]
public sealed partial class DiseaseSourceSpawnRuleComponent : Component
{
    /// <summary>
    /// Что подбрасывается на станцию.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype;

    /// <summary>
    /// Болезни, из которых выбирается одна для очага.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Diseases = new();

    /// <summary>
    /// Какими путями очаг заражает.
    /// </summary>
    [DataField]
    public DiseaseTransmissionRoute Routes = DiseaseTransmissionRoute.Contact;

    /// <summary>
    /// Шанс заражения при контакте с очагом.
    /// </summary>
    [DataField]
    public float ContactChance = 0.25f;

    /// <summary>
    /// Шанс заражения при проглатывании.
    /// </summary>
    [DataField]
    public float IngestionChance = 1f;
}
