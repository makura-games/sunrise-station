namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Вероятностные сроки смены стадии. До <see cref="MinDuration"/> смена невозможна, затем срабатывает
/// с шансом <see cref="Chance"/> на каждой проверке, а на <see cref="MaxDuration"/> происходит принудительно.
/// </summary>
/// <remarks>
/// Проверки идут раз в <see cref="DiseasePrototype.ProcessInterval"/>.
/// </remarks>
[DataDefinition]
public sealed partial class DiseaseProgression
{
    /// <summary>
    /// Сколько надо пробыть на стадии, прежде чем смена вообще станет возможна.
    /// </summary>
    [DataField]
    public TimeSpan MinDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Шанс смены на каждой проверке после <see cref="MinDuration"/>. Ноль отключает случайные смены.
    /// </summary>
    [DataField]
    public float Chance = 0.1f;

    /// <summary>
    /// Время на стадии, после которого смена принудительна. Null — смена может не наступить никогда.
    /// </summary>
    [DataField]
    public TimeSpan? MaxDuration;
}
