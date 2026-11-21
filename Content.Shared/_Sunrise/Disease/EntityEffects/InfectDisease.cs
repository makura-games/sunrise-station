using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.EntityEffects;

/// <summary>
/// Пытается заразить цель болезнью. Заражение идёт через обычные проверки,
/// поэтому иммунитет, путь передачи и защита по-прежнему учитываются.
/// </summary>
/// <remarks>
/// Обрабатывается на сервере, так как состояние болезней существует только там.
/// </remarks>
public sealed partial class InfectDisease : EntityEffectBase<InfectDisease>
{
    /// <summary>
    /// Болезнь, которой заражается цель.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<DiseasePrototype> Disease;

    /// <summary>
    /// Путь передачи, по которому выполняется попытка заражения.
    /// </summary>
    [DataField]
    public DiseaseTransmissionRoute Route = DiseaseTransmissionRoute.Ingestion;

    /// <summary>
    /// Шанс заражения до применения модификаторов защиты.
    /// </summary>
    [DataField]
    public float Chance = 1f;
}
