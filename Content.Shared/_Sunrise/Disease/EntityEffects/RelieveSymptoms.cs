using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.EntityEffects;

/// <summary>
/// Временно снимает симптомы болезней носителя. В отличие от <see cref="SuppressDisease"/>,
/// не замедляет развитие болезни и не снижает её заразность: пациент чувствует себя лучше, но остаётся заразным.
/// </summary>
/// <remarks>
/// Обрабатывается на сервере, так как состояние болезней существует только там.
/// </remarks>
public sealed partial class RelieveSymptoms : BaseDiseaseTreatmentEffect<RelieveSymptoms>
{
    /// <summary>
    /// Время облегчения, добавляемое за одно применение, умножается на масштаб эффекта.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Верхний предел накопленного времени облегчения.
    /// </summary>
    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromMinutes(2);

    /// <inheritdoc/>
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("entity-effect-guidebook-disease-relieve",
            ("chance", Probability),
            ("target", GetTargetText(prototype)),
            ("time", Duration.TotalSeconds));
    }
}
