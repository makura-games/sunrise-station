using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// О чём заявляет партия вакцины.
/// </summary>
public enum DiseaseClaimKind : byte
{
    /// <summary>
    /// Действует на определённый класс возбудителя.
    /// </summary>
    Class,

    /// <summary>
    /// Подавляет симптом.
    /// </summary>
    Suppresses,

    /// <summary>
    /// Нестабильна, если у болезни есть этот симптом.
    /// </summary>
    UnstableWith,
}

/// <summary>
/// Одно заявленное свойство на карточке пробной партии.
/// </summary>
/// <remarks>
/// Свойства собираются в рантайме из классов возбудителя и симптомов, замеченных у пациентов,
/// поэтому новый симптом в игре автоматически добавляет и свои свойства.
/// </remarks>
/// <param name="Kind">О чём говорит свойство.</param>
/// <param name="Classification">Класс возбудителя, для <see cref="DiseaseClaimKind.Class"/>.</param>
/// <param name="Symptom">Симптом, для свойств, завязанных на симптомы.</param>
public readonly record struct DiseaseClaim(
    DiseaseClaimKind Kind,
    DiseaseClassification Classification,
    ProtoId<DiseaseSymptomPrototype> Symptom);
