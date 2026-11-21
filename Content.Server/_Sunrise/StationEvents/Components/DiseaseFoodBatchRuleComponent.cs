using Content.Server._Sunrise.StationEvents.Events;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.StationEvents.Components;

/// <summary>
/// Партия брака: часть съестного на станции оказывается заражённой.
/// </summary>
/// <remarks>
/// Бьёт по кухне и автоматам, а не по всей еде в мире: заражаются только предметы,
/// лежащие на самой станции.
/// </remarks>
[RegisterComponent, Access(typeof(DiseaseFoodBatchRule))]
public sealed partial class DiseaseFoodBatchRuleComponent : Component
{
    /// <summary>
    /// Болезни, из которых выбирается одна на всю партию.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Diseases = new();

    /// <summary>
    /// Сколько порций портится минимум.
    /// </summary>
    [DataField]
    public int MinItems = 4;

    /// <summary>
    /// Сколько порций портится максимум.
    /// </summary>
    [DataField]
    public int MaxItems = 8;

    /// <summary>
    /// Шанс заражения при съедании испорченной порции.
    /// </summary>
    [DataField]
    public float IngestionChance = 1f;
}
