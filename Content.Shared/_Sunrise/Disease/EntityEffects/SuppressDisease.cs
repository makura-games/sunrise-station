using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.EntityEffects;

/// <summary>
/// Временно подавляет указанные болезни носителя: скрывает подавляемые симптомы, снижает заразность
/// и, если болезнь это допускает, останавливает развитие.
/// </summary>
/// <remarks>
/// Обрабатывается на сервере, так как состояние болезней существует только там.
/// </remarks>
public sealed partial class SuppressDisease : BaseDiseaseTreatmentEffect<SuppressDisease>
{
    /// <summary>
    /// Время подавления, добавляемое за одно применение, умножается на масштаб эффекта.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Верхний предел накопленного времени подавления.
    /// </summary>
    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("entity-effect-guidebook-disease-suppress",
            ("chance", Probability),
            ("target", GetTargetText(prototype)),
            ("time", Duration.TotalSeconds));
    }
}
