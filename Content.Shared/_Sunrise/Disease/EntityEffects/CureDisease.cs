using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.EntityEffects;

/// <summary>
/// Добавляет прогресс лечения указанным болезням носителя. Когда прогресс достигает
/// <see cref="Prototypes.DiseasePrototype.CureThreshold"/>, болезнь идёт к выздоровлению.
/// </summary>
/// <remarks>
/// Обрабатывается на сервере, так как состояние болезней существует только там.
/// </remarks>
public sealed partial class CureDisease : BaseDiseaseTreatmentEffect<CureDisease>
{
    /// <summary>
    /// Прогресс лечения, добавляемый за одно применение, умножается на масштаб эффекта.
    /// </summary>
    [DataField]
    public float Potency = 1f;

    /// <inheritdoc/>
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("entity-effect-guidebook-disease-cure",
            ("chance", Probability),
            ("target", GetTargetText(prototype)));
    }
}
