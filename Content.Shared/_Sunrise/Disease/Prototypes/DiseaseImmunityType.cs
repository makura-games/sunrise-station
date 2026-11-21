namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Какой иммунитет получает носитель после выздоровления.
/// </summary>
public enum DiseaseImmunityType : byte
{
    /// <summary>
    /// Носитель может заразиться той же болезнью сразу же.
    /// </summary>
    None,

    /// <summary>
    /// Носитель защищён на время <see cref="DiseaseImmunityPolicy.Duration"/>.
    /// </summary>
    Temporary,

    /// <summary>
    /// Носитель защищён до конца раунда.
    /// </summary>
    Permanent,
}
