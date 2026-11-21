using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Симптомы, доступные заготовке на одной стадии развития.
/// Поздние стадии собираются из более тяжёлых симптомов, поэтому партия под микроскопом усложняется.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class DiseaseStagePool
{
    /// <summary>Варианты симптомов стадии вместе с базовым весом.</summary>
    [DataField]
    public List<DiseaseSymptomEntry> Symptoms = new();

    /// <summary>
    /// Сколько симптомов стадии вирусолог развил сам: основной и усиления.
    /// По сумме усилений всех стадий считается оставшийся запас усилений заготовки.
    /// </summary>
    [DataField]
    public int Developed;
}
