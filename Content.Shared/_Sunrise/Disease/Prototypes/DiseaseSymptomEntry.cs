using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Запись пула симптомов стадии вместе с её весом.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class DiseaseSymptomEntry
{
    /// <summary>
    /// Симптом, который может проявиться на этой стадии.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<DiseaseSymptomPrototype> Symptom;

    /// <summary>
    /// Относительный вес выбора внутри пула. Тяжёлым симптомам на ранних стадиях ставьте вес поменьше.
    /// </summary>
    [DataField]
    public float Weight = 1f;
}
